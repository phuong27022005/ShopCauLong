using System.Globalization;
using System.Text.Json;
using BadmintonShop.Api.Catalog;
using BadmintonShop.Core.Entities;
using BadmintonShop.Core.Interfaces;
using Microsoft.Extensions.Options;

namespace BadmintonShop.Api.Payments;

public sealed record IpnResult(
    [property: System.Text.Json.Serialization.JsonPropertyName("RspCode")] string RspCode,
    [property: System.Text.Json.Serialization.JsonPropertyName("Message")] string Message);
public sealed record CreatePaymentRequest(int OrderId);

public sealed class VnpayService(IPaymentRepository repository, IOptions<VnpayOptions> settings, TimeProvider clock)
{
    private VnpayOptions Options => settings.Value;
    public Task<string> CreateAsync(int userId, int orderId, string ip, CancellationToken ct)
    {
        EnsureConfigured();
        return repository.TransactionAsync(async () =>
        {
            var order = await repository.LockOrderAsync(orderId, ct);
            if (order is null || order.UserId != userId) throw CatalogException.NotFound("Đơn hàng");
            var now = clock.GetUtcNow().UtcDateTime;
            if (order.Status != "Pending" || order.PaymentMethod != "VNPAY" ||
                order.PaymentStatus is "Paid" or "Refunded" || !order.PaymentExpiredAt.HasValue || order.PaymentExpiredAt <= now)
                throw CatalogException.BadRequest("Đơn hàng không thể thanh toán hoặc đã hết hạn.");
            var attempt = new PaymentTransaction
            {
                OrderId = order.Id, Amount = order.TotalAmount,
                VnpTxnRef = Guid.NewGuid().ToString("N"), CreatedAt = now
            };
            repository.Add(attempt);
            await repository.SaveAsync(ct);
            var values = new Dictionary<string, string>
            {
                ["vnp_Version"] = "2.1.0", ["vnp_Command"] = "pay", ["vnp_TmnCode"] = Options.TmnCode,
                ["vnp_Amount"] = VnpaySignature.Amount(attempt.Amount), ["vnp_CurrCode"] = "VND",
                ["vnp_TxnRef"] = attempt.VnpTxnRef, ["vnp_OrderInfo"] = "Thanh toan " + order.Code,
                ["vnp_OrderType"] = "other", ["vnp_Locale"] = "vn", ["vnp_ReturnUrl"] = Options.ReturnUrl,
                ["vnp_IpAddr"] = ip, ["vnp_CreateDate"] = now.AddHours(7).ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture),
                ["vnp_ExpireDate"] = order.PaymentExpiredAt.Value.AddHours(7).ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture)
            };
            // IPN URL được đăng ký với VNPay, không phải tham số của URL pay.
            return Options.PaymentUrl + "?" + VnpaySignature.Canonical(values) + "&vnp_SecureHash=" + VnpaySignature.Sign(values, Options.HashSecret);
        }, ct);
    }
    public async Task<IpnResult> IpnAsync(IReadOnlyDictionary<string, string> values, CancellationToken ct)
    {
        if (!VnpaySignature.Verify(values, Options.HashSecret)) return new("97", "Invalid signature");
        if (values.GetValueOrDefault("vnp_TmnCode") != Options.TmnCode) return new("97", "Invalid merchant");
        var reference = values.GetValueOrDefault("vnp_TxnRef") ?? "";
        return await repository.TransactionAsync(async () =>
        {
            var attempt = await repository.FindAttemptAsync(reference, ct);
            if (attempt is null) return new IpnResult("01", "Order not found");
            var order = await repository.LockOrderAsync(attempt.OrderId, ct);
            if (order is null) return new IpnResult("01", "Order not found");
            attempt = (await repository.FindAttemptAsync(reference, ct))!;
            if (values.GetValueOrDefault("vnp_Amount") != VnpaySignature.Amount(attempt.Amount) || order.TotalAmount != attempt.Amount)
                return new IpnResult("04", "Invalid amount");
            if (attempt.ResponseCode is not null || order.PaymentStatus is "Paid" or "Refunded")
                return new IpnResult("02", "Order already confirmed");
            if (order.PaymentMethod != "VNPAY" || order.Status != "Pending" ||
                !order.PaymentExpiredAt.HasValue || order.PaymentExpiredAt <= clock.GetUtcNow().UtcDateTime)
                return new IpnResult("99", "Order expired or closed");
            var response = values.GetValueOrDefault("vnp_ResponseCode");
            var transactionStatus = values.GetValueOrDefault("vnp_TransactionStatus");
            var transactionNo = values.GetValueOrDefault("vnp_TransactionNo");
            if (response is null || response.Length != 2 || transactionStatus is null || transactionStatus.Length != 2 ||
                string.IsNullOrWhiteSpace(transactionNo) || transactionNo.Length > 100)
                return new IpnResult("99", "Invalid callback");
            attempt.ResponseCode = response;
            attempt.VnpTransactionNo = transactionNo;
            attempt.RawData = JsonSerializer.Serialize(values);
            order.PaymentStatus = response == "00" && transactionStatus == "00" ? "Paid" : "Failed";
            await repository.SaveAsync(ct);
            return new IpnResult("00", "Confirm Success");
        }, ct);
    }
    public async Task<string> ReturnAsync(IReadOnlyDictionary<string, string> values, CancellationToken ct)
    {
        EnsureConfigured();
        var valid = VnpaySignature.Verify(values, Options.HashSecret) && values.GetValueOrDefault("vnp_TmnCode") == Options.TmnCode;
        var result = !valid ? "invalid-signature" : values.GetValueOrDefault("vnp_ResponseCode") == "00"
            && values.GetValueOrDefault("vnp_TransactionStatus") == "00" ? "pending-verification" : "failed";
        var attempt = valid
            ? await repository.FindAttemptAsync(values.GetValueOrDefault("vnp_TxnRef") ?? "", ct)
            : null;
        return Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString(Options.FrontendReturnUrl,
            new Dictionary<string, string?> { ["paymentResult"] = result, ["orderId"] = attempt?.OrderId.ToString(CultureInfo.InvariantCulture) });
    }
    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(Options.TmnCode) || string.IsNullOrWhiteSpace(Options.HashSecret)
            || !Uri.TryCreate(Options.PaymentUrl, UriKind.Absolute, out var payment)
            || payment.Scheme != "https" || payment.Host != "sandbox.vnpayment.vn"
            || !Uri.TryCreate(Options.ReturnUrl, UriKind.Absolute, out _)
            || !Uri.TryCreate(Options.IpnUrl, UriKind.Absolute, out _)
            || !Uri.TryCreate(Options.FrontendReturnUrl, UriKind.Absolute, out _))
            throw new CatalogException(503, "Chưa cấu hình đầy đủ VNPay sandbox.");
    }
}
