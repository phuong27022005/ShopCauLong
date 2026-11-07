using BadmintonShop.Api.Catalog;
using BadmintonShop.Core.DTOs;
using BadmintonShop.Core.Entities;
using BadmintonShop.Core.Interfaces;

namespace BadmintonShop.Api.Engagement;

public sealed class CouponService(IEngagementRepository repository, TimeProvider clock)
{
    public async Task<(Coupon Coupon, decimal Discount)> ValidateAsync(string code, decimal subtotal, CancellationToken ct)
    {
        var coupon=await repository.CouponByCodeAsync(code.Trim().ToUpperInvariant(),ct);
        var now=clock.GetUtcNow().UtcDateTime;
        if (coupon is null || !coupon.IsActive) throw CatalogException.BadRequest("Mã giảm giá không tồn tại hoặc đã tắt.");
        if (now<coupon.StartAt) throw CatalogException.BadRequest("Mã giảm giá chưa tới thời gian sử dụng.");
        if (now>=coupon.EndAt) throw CatalogException.BadRequest("Mã giảm giá đã hết hạn.");
        if (coupon.UsedCount>=coupon.UsageLimit) throw CatalogException.BadRequest("Mã giảm giá đã hết lượt.");
        if (subtotal<coupon.MinOrderAmount) throw CatalogException.BadRequest("Đơn hàng chưa đạt giá trị tối thiểu.");
        return (coupon,CouponRules.Discount(coupon,subtotal));
    }
    public async Task ApplyAsync(Order order, string? code, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code)) return;
        var (coupon,discount)=await ValidateAsync(code,order.TotalAmount,ct);
        order.CouponId=coupon.Id;
        order.CouponCode=coupon.Code;
        order.DiscountAmount=discount;
        order.TotalAmount-=discount;
        coupon.UsedCount++;
        // Save thuộc transaction tạo đơn, RowVersion bảo vệ lượt dùng cuối.
    }
    public async Task ReleaseAsync(Order order, CancellationToken ct)
    {
        if (!order.CouponId.HasValue) return;
        var coupon=await repository.CouponAsync(order.CouponId.Value,ct)
            ?? throw new InvalidOperationException("Không tìm thấy coupon của đơn.");
        if (coupon.UsedCount<=0) throw new InvalidOperationException("Lượt dùng coupon không nhất quán.");
        coupon.UsedCount--;
    }
    public async Task<List<CouponDto>> ListAsync(CancellationToken ct) => (await repository.CouponsAsync(ct)).Select(Map).ToList();
    public async Task<CouponDto> GetAsync(int id,CancellationToken ct) =>
        Map(await repository.CouponAsync(id,ct) ?? throw CatalogException.NotFound("Mã giảm giá"));
    public async Task<CouponDto> SaveAsync(int? id,CouponRequest input,CancellationToken ct)
    {
        var code=input.Code.Trim().ToUpperInvariant();
        var existing=await repository.CouponByCodeAsync(code,ct);
        if (existing is not null && existing.Id!=id) throw CatalogException.Conflict("Mã giảm giá đã tồn tại.");
        var coupon=id.HasValue ? await repository.CouponAsync(id.Value,ct) ?? throw CatalogException.NotFound("Mã giảm giá") : new Coupon();
        if (input.UsageLimit<coupon.UsedCount) throw CatalogException.BadRequest("Giới hạn không được nhỏ hơn số lượt đã dùng.");
        coupon.Code=code; coupon.Type=input.Type; coupon.Value=input.Value; coupon.MinOrderAmount=input.MinOrderAmount;
        coupon.MaxDiscount=input.MaxDiscount; coupon.UsageLimit=input.UsageLimit; coupon.StartAt=input.StartAt.ToUniversalTime();
        coupon.EndAt=input.EndAt.ToUniversalTime(); coupon.IsActive=input.IsActive;
        if (!id.HasValue) repository.Add(coupon);
        await repository.SaveAsync(ct);
        return Map(coupon);
    }
    public async Task DeleteAsync(int id,CancellationToken ct)
    {
        var coupon=await repository.CouponAsync(id,ct) ?? throw CatalogException.NotFound("Mã giảm giá");
        if (await repository.CouponReferencedAsync(id,ct)) coupon.IsActive=false;
        else repository.Remove(coupon);
        await repository.SaveAsync(ct);
    }
    private static CouponDto Map(Coupon x) => new(x.Id,x.Code,x.Type,x.Value,x.MinOrderAmount,x.MaxDiscount,x.UsageLimit,x.UsedCount,x.StartAt,x.EndAt,x.IsActive);
}
