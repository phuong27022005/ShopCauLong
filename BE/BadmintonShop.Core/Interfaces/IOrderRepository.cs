using BadmintonShop.Core.DTOs;
using BadmintonShop.Core.Entities;

namespace BadmintonShop.Core.Interfaces;

public interface IOrderRepository
{
    Task<T> TransactionAsync<T>(Func<Task<T>> action, CancellationToken ct);
    Task<List<CartItem>> CartItemsAsync(int userId, CancellationToken ct);
    Task<List<Product>> ProductsAsync(IEnumerable<int> ids, CancellationToken ct);
    Task<Order?> FindAsync(int id, int? userId, CancellationToken ct);
    Task<List<OrderItem>> LinesAsync(int id, CancellationToken ct);
    Task<PageDto<Order>> ListAsync(int? userId, OrderQuery query, CancellationToken ct);
    void Add(Order order);
    void Add(OrderItem item);
    void ClearCart(IEnumerable<CartItem> items);
    Task SaveAsync(CancellationToken ct);
}
public sealed class OrderConflictException(Exception cause) : Exception("Dữ liệu đã thay đổi. Vui lòng tải lại và thử lại.", cause);

