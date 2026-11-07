using BadmintonShop.Core.Entities;
using BadmintonShop.Core.Interfaces;
using BadmintonShop.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BadmintonShop.Infrastructure.Repositories;

public sealed class PaymentRepository(ShopDbContext db) : IPaymentRepository
{
    public async Task<T> TransactionAsync<T>(Func<Task<T>> action, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var result = await action();
            await transaction.CommitAsync(ct);
            return result;
        }
        finally
        {
            // Mỗi đơn trong một lượt job dùng dữ liệu mới.
            db.ChangeTracker.Clear();
        }
    }
    public Task<Order?> LockOrderAsync(int id, CancellationToken ct) =>
        db.Database.IsSqlServer()
            ? db.Orders.FromSqlInterpolated($"SELECT * FROM [Orders] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {id}").SingleOrDefaultAsync(ct)
            : db.Orders.SingleOrDefaultAsync(x => x.Id == id, ct);
    public async Task<PaymentTransaction?> FindAttemptAsync(string reference, CancellationToken ct)
    {
        var attempt = await db.PaymentTransactions.SingleOrDefaultAsync(x => x.VnpTxnRef == reference, ct);
        if (attempt is not null) await db.Entry(attempt).ReloadAsync(ct);
        return attempt;
    }
    public void Add(PaymentTransaction transaction) => db.PaymentTransactions.Add(transaction);
    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
    public Task<List<int>> ExpiredAsync(DateTime now, CancellationToken ct) =>
        db.Orders.AsNoTracking().Where(x => x.PaymentMethod == "VNPAY" &&
            (x.Status == "Pending" || x.Status == "Confirmed") &&
            (x.PaymentStatus == "Unpaid" || x.PaymentStatus == "Failed") && x.PaymentExpiredAt <= now)
            .OrderBy(x => x.Id).Select(x => x.Id).Take(100).ToListAsync(ct);
    public async Task RestoreStockAsync(int orderId, CancellationToken ct)
    {
        var lines = await db.OrderItems.Where(x => x.OrderId == orderId).ToListAsync(ct);
        var ids = lines.Select(x => x.ProductId).ToArray();
        var products = await db.Products.Where(x => ids.Contains(x.Id)).OrderBy(x => x.Id).ToListAsync(ct);
        foreach (var line in lines)
        {
            var product = products.Single(x => x.Id == line.ProductId);
            product.Stock = checked(product.Stock + line.Quantity);
        }
    }
}
