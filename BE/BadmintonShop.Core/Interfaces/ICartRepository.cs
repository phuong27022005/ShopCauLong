using BadmintonShop.Core.Entities;

namespace BadmintonShop.Core.Interfaces;

public interface ICartRepository
{
    Task<T> WithCartAsync<T>(int userId, Func<Cart, List<CartItem>, Task<T>> action, CancellationToken ct);
    Task<List<Product>> ProductsAsync(IEnumerable<int> ids, CancellationToken ct);
    void Add(CartItem item);
    void Remove(CartItem item);
}
public sealed class CartUserNotFoundException : Exception;

