namespace BadmintonShop.Core.Entities;

public sealed class Coupon
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Type { get; set; } = "";
    public decimal Value { get; set; }
    public decimal MinOrderAmount { get; set; }
    public decimal? MaxDiscount { get; set; }
    public int UsageLimit { get; set; }
    public int UsedCount { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public bool IsActive { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
public static class CouponRules
{
    public static decimal Discount(Coupon coupon, decimal subtotal)
    {
        var discount = coupon.Type == "Percent" ? subtotal * coupon.Value / 100 : coupon.Value;
        if (coupon.MaxDiscount.HasValue) discount = Math.Min(discount, coupon.MaxDiscount.Value);
        return Math.Round(Math.Clamp(discount, 0, subtotal), 2, MidpointRounding.AwayFromZero);
    }
}
