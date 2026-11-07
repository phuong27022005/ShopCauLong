namespace BadmintonShop.Core.DTOs;

public sealed record ProductDto(int Id, string Name, string Slug, decimal Price, decimal? DiscountPrice);
