namespace BadmintonShop.Api.Payments;

public sealed class VnpayOptions
{
    public string TmnCode { get; set; } = "";
    public string HashSecret { get; set; } = "";
    public string PaymentUrl { get; set; } = "";
    public string ReturnUrl { get; set; } = "";
    public string IpnUrl { get; set; } = "";
    public string FrontendReturnUrl { get; set; } = "";
    public bool ExpirationJobEnabled { get; set; } = true;
}
