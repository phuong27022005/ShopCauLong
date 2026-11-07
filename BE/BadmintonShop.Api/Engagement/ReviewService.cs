using BadmintonShop.Api.Catalog;
using BadmintonShop.Core.DTOs;
using BadmintonShop.Core.Entities;
using BadmintonShop.Core.Interfaces;

namespace BadmintonShop.Api.Engagement;

public sealed class ReviewService(IEngagementRepository repository)
{
    public async Task<object> EligibilityAsync(int userId, int productId, CancellationToken ct)
    {
        var purchased = await repository.HasPurchasedAsync(userId, productId, ct);
        var own = await repository.OwnReviewAsync(userId, productId, ct);
        return new { canReview = purchased && own is null,
            reason = own is not null ? "Bạn đã đánh giá sản phẩm này." : purchased ? "" : "Chỉ khách có đơn Completed chứa sản phẩm mới được đánh giá." };
    }
    public Task<PageDto<ReviewDto>> AdminListAsync(int page, int pageSize, CancellationToken ct)
    {
        if (page < 1 || pageSize is < 1 or > 50) throw CatalogException.BadRequest("Phân trang không hợp lệ.");
        return repository.AllReviewsAsync(page, pageSize, ct);
    }
    public async Task<ReviewPage> ListAsync(int productId, int page, int pageSize, CancellationToken ct)
    {
        if (page < 1 || pageSize is < 1 or > 50) throw CatalogException.BadRequest("page phải >= 1, pageSize từ 1 đến 50.");
        if (!await repository.ProductExistsAsync(productId,ct)) throw CatalogException.NotFound("Sản phẩm");
        return await repository.ReviewsAsync(productId,page,pageSize,ct);
    }
    public async Task<ReviewDto> SaveAsync(int userId, int productId, int? id, ReviewRequest request, CancellationToken ct)
    {
        if (!await repository.HasPurchasedAsync(userId,productId,ct))
            throw new CatalogException(403,"Chỉ khách có đơn Completed chứa sản phẩm mới được đánh giá.");
        var review = id.HasValue ? await repository.ReviewAsync(id.Value,ct) : null;
        if (id.HasValue && (review is null || review.UserId != userId || review.ProductId != productId))
            throw CatalogException.NotFound("Đánh giá");
        if (!id.HasValue && await repository.HasReviewAsync(userId,productId,ct))
            throw CatalogException.Conflict("Bạn đã đánh giá sản phẩm này.");
        review ??= new Review { UserId=userId, ProductId=productId, CreatedAt=DateTime.UtcNow };
        review.Rating=request.Rating;
        review.Comment=request.Comment.Trim();
        if (!id.HasValue) repository.Add(review);
        await repository.SaveAsync(ct);
        return new(review.Id,review.UserId,review.ProductId,review.Rating,review.Comment,review.CreatedAt);
    }
    public async Task DeleteAsync(int userId, bool admin, int productId, int id, CancellationToken ct)
    {
        var review=await repository.ReviewAsync(id,ct);
        if (review is null || review.ProductId!=productId) throw CatalogException.NotFound("Đánh giá");
        if (!admin && (review.UserId!=userId || !await repository.HasPurchasedAsync(userId,productId,ct)))
            throw new CatalogException(403,"Bạn không được xóa đánh giá này.");
        repository.Remove(review);
        await repository.SaveAsync(ct);
    }
}
