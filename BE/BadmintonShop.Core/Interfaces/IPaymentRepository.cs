using BadmintonShop.Core.Entities;

namespace BadmintonShop.Core.Interfaces;

public interface IPaymentRepository
{
    Task<T> TransactionAsync<T>(Func<Task<T>> action, CancellationToken ct);
    Task<Order?> LockOrderAsync(int id, CancellationToken ct);
    Task<PaymentTransaction?> FindAttemptAsync(string reference, CancellationToken ct);
    void Add(PaymentTransaction transaction);
    Task SaveAsync(CancellationToken ct);
    Task<List<int>> ExpiredAsync(DateTime now, CancellationToken ct);
    Task RestoreStockAsync(int orderId, CancellationToken ct);
}
