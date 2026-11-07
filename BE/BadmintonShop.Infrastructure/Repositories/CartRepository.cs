using System.Data;
using BadmintonShop.Core.Entities;
using BadmintonShop.Core.Interfaces;
using BadmintonShop.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BadmintonShop.Infrastructure.Repositories;

public sealed class CartRepository(ShopDbContext db) : ICartRepository
{
    public async Task<T> WithCartAsync<T>(int userId, Func<Cart, List<CartItem>, Task<T>> action, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        // Khóa theo user để các request cùng giỏ không ghi đè số lượng.
        var user = db.Database.IsSqlServer()
            ? await db.Users.FromSqlInterpolated($"SELECT * FROM [Users] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {userId}").SingleOrDefaultAsync(ct)
            : await db.Users.SingleOrDefaultAsync(x => x.Id == userId, ct);
        if (user is null) throw new CartUserNotFoundException();

        var cart = await db.Carts.SingleOrDefaultAsync(x => x.UserId == userId, ct);
        if (cart is null)
        {
            cart = new Cart { UserId = userId };
            db.Carts.Add(cart);
            await db.SaveChangesAsync(ct);
        }
        var items = await db.CartItems.Where(x => x.CartId == cart.Id).OrderBy(x => x.Id).ToListAsync(ct);
        var result = await action(cart, items);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return result;
    }

    public Task<List<Product>> ProductsAsync(IEnumerable<int> ids, CancellationToken ct) =>
        db.Products.AsNoTracking().Where(x => ids.Contains(x.Id)).ToListAsync(ct);
    public void Add(CartItem item) => db.CartItems.Add(item);
    public void Remove(CartItem item) => db.CartItems.Remove(item);
}

