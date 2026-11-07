using BadmintonShop.Core.DTOs;
using BadmintonShop.Core.Entities;
using BadmintonShop.Core.Interfaces;
using BadmintonShop.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BadmintonShop.Infrastructure.Repositories;

public sealed class OrderRepository(ShopDbContext db) : IOrderRepository
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
        catch (DbUpdateConcurrencyException ex) { throw new OrderConflictException(ex); }
    }
    public Task<List<CartItem>> CartItemsAsync(int userId, CancellationToken ct) =>
        (from item in db.CartItems join cart in db.Carts on item.CartId equals cart.Id
         where cart.UserId == userId orderby item.ProductId select item).ToListAsync(ct);
    public Task<List<Product>> ProductsAsync(IEnumerable<int> ids, CancellationToken ct) =>
        db.Products.Where(x => ids.Contains(x.Id)).OrderBy(x => x.Id).ToListAsync(ct);
    public Task<Order?> FindAsync(int id, int? userId, CancellationToken ct) =>
        db.Orders.SingleOrDefaultAsync(x => x.Id == id && (!userId.HasValue || x.UserId == userId), ct);
    public Task<List<OrderItem>> LinesAsync(int id, CancellationToken ct) =>
        db.OrderItems.AsNoTracking().Where(x => x.OrderId == id).OrderBy(x => x.Id).ToListAsync(ct);
    public async Task<PageDto<Order>> ListAsync(int? userId, OrderQuery q, CancellationToken ct)
    {
        var query = db.Orders.AsNoTracking().Where(x => !userId.HasValue || x.UserId == userId);
        if (q.Status is not null) query = query.Where(x => x.Status == q.Status);
        if (q.FromDate.HasValue) query = query.Where(x => x.CreatedAt >= q.FromDate);
        if (q.ToDate.HasValue) query = query.Where(x => x.CreatedAt <= q.ToDate);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var term = q.Search.Trim();
            query = query.Where(x => x.Code!.Contains(term) || x.Phone.Contains(term));
        }
        var count = await query.CountAsync(ct);
        var skip = ((long)q.Page - 1) * q.PageSize;
        var items = skip >= count ? [] : await query.OrderByDescending(x => x.Id).Skip((int)skip).Take(q.PageSize).ToListAsync(ct);
        return new PageDto<Order>(items, q.Page, q.PageSize, count);
    }
    public void Add(Order order) => db.Orders.Add(order);
    public void Add(OrderItem item) => db.OrderItems.Add(item);
    public void ClearCart(IEnumerable<CartItem> items) => db.CartItems.RemoveRange(items);
    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}

