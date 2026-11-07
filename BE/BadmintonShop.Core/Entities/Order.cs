namespace BadmintonShop.Core.Entities;

public sealed class Order
{
    public byte[] RowVersion { get; set; } = [];
    public string PaymentStatus { get; set; } = "Unpaid";
    public DateTime? PaymentExpiredAt { get; set; }
    public int? CouponId { get; set; }
    public string? CouponCode { get; set; }
    public decimal DiscountAmount { get; set; }
    public int Id { get; set; }
    public string? Code { get; set; }
    public string? FullName { get; set; }
    public string? Note { get; set; }
    public int UserId { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string ShippingAddress { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
