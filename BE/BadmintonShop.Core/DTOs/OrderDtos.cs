namespace BadmintonShop.Core.DTOs;

public sealed record CreateOrderRequest(string ShippingAddress, string Phone, string PaymentMethod, string? Note, string? FullName = null, string? CouponCode = null);
public sealed record UpdateOrderStatusRequest(string Status);
public sealed class OrderQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 12;
    public string? Status { get; init; }
    public DateTime? FromDate { get; init; }
    public DateTime? ToDate { get; init; }
    public string? Search { get; init; }
}
public sealed record OrderLineDto(int ProductId, string ProductName, int Quantity, decimal UnitPrice, decimal LineTotal);
public sealed record OrderDto(int Id, string? Code, string Status, decimal TotalAmount, string ShippingAddress,
    string Phone, string PaymentMethod, string? Note, DateTime CreatedAt, IReadOnlyList<OrderLineDto> Items, string? FullName = null, string PaymentStatus = "Unpaid", DateTime? PaymentExpiredAt = null, string? CouponCode = null, decimal DiscountAmount = 0);

