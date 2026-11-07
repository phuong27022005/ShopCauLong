using System.Security.Claims;
using BadmintonShop.Api.Catalog;
using BadmintonShop.Api.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BadmintonShop.Api.Controllers;

[ApiController]
[Route("api/payments/vnpay")]
public sealed class PaymentsController(VnpayService payments, ILogger<PaymentsController> logger) : ControllerBase
{
    [Authorize]
    [HttpPost("create")]
    public async Task<IActionResult> Create(CreatePaymentRequest input, CancellationToken ct)
    {
        if (!int.TryParse(User.FindFirstValue("sub"), out var userId) || userId <= 0) return Unauthorized(ApiResponse.Error("Token không hợp lệ."));
        var ip = HttpContext.Connection.RemoteIpAddress?.MapToIPv4().ToString() ?? "127.0.0.1";
        return Ok(ApiResponse.Ok(new { paymentUrl = await payments.CreateAsync(userId, input.OrderId, ip, ct) }));
    }
    [HttpGet("return")]
    public async Task<IActionResult> Return(CancellationToken ct) =>
        Redirect(await payments.ReturnAsync(Parameters() ?? new Dictionary<string, string>(), ct));
    [HttpGet("ipn")]
    public async Task<IActionResult> Ipn(CancellationToken ct)
    {
        var values = Parameters();
        if (values is null) return Ok(new IpnResult("97", "Invalid signature"));
        try { return Ok(await payments.IpnAsync(values, ct)); }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogError(ex, "Không xử lý được IPN.");
            return Ok(new IpnResult("99", "Internal error"));
        }
    }
    private Dictionary<string, string>? Parameters()
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in Request.Query.Where(x => x.Key.StartsWith("vnp_", StringComparison.Ordinal)))
        {
            if (pair.Value.Count != 1) return null;
            values.Add(pair.Key, pair.Value.ToString());
        }
        return values;
    }
}
