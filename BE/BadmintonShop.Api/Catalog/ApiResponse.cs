namespace BadmintonShop.Api.Catalog;

public sealed record ApiResponse(bool Success, object? Data, string Message)
{
    public static ApiResponse Ok(object? data = null, string message = "Thành công.") => new(true, data, message);
    public static ApiResponse Error(string message, object? data = null) => new(false, data, message);
}
