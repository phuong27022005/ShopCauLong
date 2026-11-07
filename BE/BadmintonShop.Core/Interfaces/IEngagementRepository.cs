using BadmintonShop.Core.DTOs;
using BadmintonShop.Core.Entities;

namespace BadmintonShop.Core.Interfaces;

public interface IEngagementRepository
{
    Task<Review?> OwnReviewAsync(int userId, int productId, CancellationToken ct);
    Task<PageDto<ReviewDto>> AllReviewsAsync(int page, int pageSize, CancellationToken ct);
    Task<bool> ProductExistsAsync(int id, CancellationToken ct);
    Task<bool> HasPurchasedAsync(int userId, int productId, CancellationToken ct);
    Task<ReviewPage> ReviewsAsync(int productId, int page, int pageSize, CancellationToken ct);
    Task<Review?> ReviewAsync(int id, CancellationToken ct);
    Task<bool> HasReviewAsync(int userId, int productId, CancellationToken ct);
    Task<Coupon?> CouponAsync(int id, CancellationToken ct);
    Task<Coupon?> CouponByCodeAsync(string code, CancellationToken ct);
    Task<List<Coupon>> CouponsAsync(CancellationToken ct);
    Task<bool> CouponReferencedAsync(int id, CancellationToken ct);
    void Add(Review review);
    void Remove(Review review);
    void Add(Coupon coupon);
    void Remove(Coupon coupon);
    Task SaveAsync(CancellationToken ct);
}
public sealed class EngagementConflictException : Exception;

