namespace BadmintonShop.Core.Entities;

public sealed class OrderItem
{
    public string ProductName { get; set; } = "";
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    // Lưu giá tại thời điểm đặt hàng.
    public decimal UnitPrice { get; set; }
}
