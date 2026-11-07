namespace BadmintonShop.Core.DTOs;

public sealed record ReviewRequest(int Rating, string Comment);
public sealed record ReviewDto(int Id, int UserId, int ProductId, int Rating, string Comment, DateTime CreatedAt);
public sealed record ReviewPage(IReadOnlyList<ReviewDto> Items, int Page, int PageSize, int TotalItems, int TotalPages, double AverageRating);
public sealed record CouponRequest(string Code, string Type, decimal Value, decimal MinOrderAmount,
    decimal? MaxDiscount, int UsageLimit, DateTime StartAt, DateTime EndAt, bool IsActive);
public sealed record CouponDto(int Id, string Code, string Type, decimal Value, decimal MinOrderAmount,
    decimal? MaxDiscount, int UsageLimit, int UsedCount, DateTime StartAt, DateTime EndAt, bool IsActive);
public sealed record ValidateCouponRequest(string Code, decimal Subtotal);

