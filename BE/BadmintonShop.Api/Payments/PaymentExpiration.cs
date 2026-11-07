using BadmintonShop.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BadmintonShop.Api.Payments;

public sealed class PaymentExpiration(IPaymentRepository repository, TimeProvider clock, ILogger<PaymentExpiration> logger, BadmintonShop.Api.Engagement.CouponService coupons)
{
    public async Task RunAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        foreach (var id in await repository.ExpiredAsync(now, ct))
        {
            try
            {
                await repository.TransactionAsync(async () =>
                {
                    var order = await repository.LockOrderAsync(id, ct);
                    if (order is null || order.PaymentMethod != "VNPAY" || order.Status is not ("Pending" or "Confirmed") ||
                        order.PaymentStatus is not ("Unpaid" or "Failed") || !order.PaymentExpiredAt.HasValue || order.PaymentExpiredAt > now)
                        return false;
                    await repository.RestoreStockAsync(id, ct);
                    await coupons.ReleaseAsync(order,ct);
                    order.Status = "Cancelled";
                    await repository.SaveAsync(ct);
                    return true;
                }, ct);
            }
            catch (DbUpdateConcurrencyException ex) { logger.LogInformation(ex, "Đơn {OrderId} thay đổi trong lúc hết hạn.", id); }
        }
    }
}
public sealed class PaymentExpirationWorker(IServiceScopeFactory scopes, IOptions<VnpayOptions> options,
    ILogger<PaymentExpirationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.ExpirationJobEnabled) return;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<PaymentExpiration>().RunAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Không xử lý được đơn VNPay hết hạn."); }
        }
    }
}
