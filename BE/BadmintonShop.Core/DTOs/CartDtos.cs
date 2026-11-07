namespace BadmintonShop.Core.DTOs;

public sealed record AddCartItemRequest(int ProductId, int Quantity);
public sealed record UpdateCartItemRequest(int Quantity);
public sealed record MergeCartRequest(List<AddCartItemRequest> Items);
public sealed record CartProductDto(int Id, string Name, string Slug, string? ImageUrl,
    decimal Price, decimal? DiscountPrice, int Stock, bool IsActive);
public sealed record CartLineDto(int ProductId, int Quantity, CartProductDto Product,
    decimal UnitPrice, decimal LineTotal, bool IsAvailable);
public sealed record CartDto(IReadOnlyList<CartLineDto> Items, decimal TotalAmount);

