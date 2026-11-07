namespace BadmintonShop.Api.Catalog;

public sealed class CatalogException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;

    public static CatalogException BadRequest(string message) => new(400, message);
    public static CatalogException NotFound(string resource) => new(404, $"{resource} không tồn tại.");
    public static CatalogException Conflict(string message) => new(409, message);
}
