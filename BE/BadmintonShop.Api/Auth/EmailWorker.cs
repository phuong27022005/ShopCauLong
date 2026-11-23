using BadmintonShop.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BadmintonShop.Api.Auth;

public sealed class EmailWorker(IServiceScopeFactory scopes, IConfiguration configuration,
    ILogger<EmailWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("Email:WorkerEnabled", true)) return;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<ShopDbContext>();
                var now = DateTime.UtcNow;
                var messages = await db.OutgoingEmails.Where(x => x.SentAt == null && x.Attempts < 10 && x.NextAttemptAt <= now)
                    .OrderBy(x => x.Id).Take(10).ToListAsync(stoppingToken);
                foreach (var message in messages)
                {
                    // A database lease prevents multiple application instances from sending the same batch.
                    var claimed = await db.OutgoingEmails.Where(x => x.Id == message.Id && x.SentAt == null && x.NextAttemptAt <= now)
                        .ExecuteUpdateAsync(s => s.SetProperty(x => x.NextAttemptAt, now.AddMinutes(2)), stoppingToken);
                    if (claimed != 1) continue;
                    try
                    {
                        await scope.ServiceProvider.GetRequiredService<EmailDelivery>().SendAsync(message, stoppingToken);
                        message.SentAt = DateTime.UtcNow;
                        message.Body = ""; // Do not retain action links after delivery.
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        logger.LogWarning("Email {MessageId} failed ({ErrorType}); will retry.", message.Id, ex.GetType().Name);
                    }
                    message.Attempts++;
                    message.NextAttemptAt = DateTime.UtcNow.AddMinutes(Math.Min(60, Math.Pow(2, message.Attempts)));
                    await db.SaveChangesAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError("Email worker failed ({ErrorType}).", ex.GetType().Name); }
        }
    }
}
