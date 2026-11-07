using BadmintonShop.Core.DTOs;
using BadmintonShop.Core.Entities;
using BadmintonShop.Core.Interfaces;
using BadmintonShop.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BadmintonShop.Infrastructure.Repositories;

public sealed class EngagementRepository(ShopDbContext db) : IEngagementRepository
{
    public Task<Review?> OwnReviewAsync(int userId, int productId, CancellationToken ct) =>
        db.Reviews.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == userId && x.ProductId == productId, ct);
    public async Task<PageDto<ReviewDto>> AllReviewsAsync(int page, int pageSize, CancellationToken ct)
    {
        var query = db.Reviews.AsNoTracking();
        var count = await query.CountAsync(ct);
        var offset = ((long)page - 1) * pageSize;
        var items = offset >= count ? [] : await query.OrderByDescending(x => x.Id).Skip((int)offset).Take(pageSize)
            .Select(x => new ReviewDto(x.Id, x.UserId, x.ProductId, x.Rating, x.Comment, x.CreatedAt)).ToListAsync(ct);
        return new(items, page, pageSize, count);
    }
    public Task<bool> ProductExistsAsync(int id, CancellationToken ct) => db.Products.AnyAsync(x => x.Id == id && x.IsActive, ct);
    public Task<bool> HasPurchasedAsync(int userId, int productId, CancellationToken ct) =>
        (from item in db.OrderItems join order in db.Orders on item.OrderId equals order.Id
         where order.UserId == userId && order.Status == "Completed" && item.ProductId == productId select item).AnyAsync(ct);
    public async Task<ReviewPage> ReviewsAsync(int productId, int page, int pageSize, CancellationToken ct)
    {
        var query = db.Reviews.AsNoTracking().Where(x => x.ProductId == productId);
        var count = await query.CountAsync(ct);
        var average = await query.Select(x => (double?)x.Rating).AverageAsync(ct) ?? 0;
        var skip = ((long)page - 1) * pageSize;
        var items = skip >= count ? [] : await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Skip((int)skip).Take(pageSize).Select(x => new ReviewDto(x.Id,x.UserId,x.ProductId,x.Rating,x.Comment,x.CreatedAt)).ToListAsync(ct);
        return new(items,page,pageSize,count,(int)Math.Ceiling((double)count/pageSize),average);
    }
    public Task<Review?> ReviewAsync(int id, CancellationToken ct) => db.Reviews.SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<bool> HasReviewAsync(int userId, int productId, CancellationToken ct) =>
        db.Reviews.AnyAsync(x => x.UserId == userId && x.ProductId == productId, ct);
    public Task<Coupon?> CouponAsync(int id, CancellationToken ct) => db.Coupons.SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<Coupon?> CouponByCodeAsync(string code, CancellationToken ct) => db.Coupons.SingleOrDefaultAsync(x => x.Code == code, ct);
    public Task<List<Coupon>> CouponsAsync(CancellationToken ct) => db.Coupons.AsNoTracking().OrderBy(x => x.Id).ToListAsync(ct);
    public Task<bool> CouponReferencedAsync(int id, CancellationToken ct) => db.Orders.AnyAsync(x => x.CouponId == id, ct);
    public void Add(Review review) => db.Reviews.Add(review);
    public void Remove(Review review) => db.Reviews.Remove(review);
    public void Add(Coupon coupon) => db.Coupons.Add(coupon);
    public void Remove(Coupon coupon) => db.Coupons.Remove(coupon);
    public async Task SaveAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            // Kiểm tra dữ liệu đã commit để nhận diện xung đột unique do request đồng thời.
            foreach (var entry in db.ChangeTracker.Entries<Review>().Where(x => x.State == EntityState.Added))
                if (await db.Reviews.AsNoTracking().AnyAsync(x => x.UserId == entry.Entity.UserId && x.ProductId == entry.Entity.ProductId, ct))
                    throw new EngagementConflictException();
            foreach (var entry in db.ChangeTracker.Entries<Coupon>().Where(x => x.State is EntityState.Added or EntityState.Modified))
                if (await db.Coupons.AsNoTracking().AnyAsync(x => x.Code == entry.Entity.Code && x.Id != entry.Entity.Id, ct))
                    throw new EngagementConflictException();
            throw;
        }
    }
}
