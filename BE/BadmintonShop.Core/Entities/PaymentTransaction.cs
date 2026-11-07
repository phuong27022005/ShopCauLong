namespace BadmintonShop.Core.Entities;

public sealed class PaymentTransaction
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public decimal Amount { get; set; }
    public string VnpTxnRef { get; set; } = "";
    public string? VnpTransactionNo { get; set; }
    public string? ResponseCode { get; set; }
    public string? RawData { get; set; }
    public DateTime CreatedAt { get; set; }
}
