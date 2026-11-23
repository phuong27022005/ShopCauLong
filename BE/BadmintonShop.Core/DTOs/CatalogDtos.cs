namespace BadmintonShop.Core.DTOs;

public sealed record BrandDto(int Id, string Name, string? Logo);
public sealed record CategoryDto(int Id, string Name, string Slug);

public sealed record ProductListDto(
    int Id, string Name, string Slug, decimal Price, decimal? DiscountPrice, string? ImageUrl,
    string BrandName, int Stock);

public sealed record ProductDetailDto(
    int Id, string Name, string Slug, string? Description, decimal Price, decimal? DiscountPrice,
    int Stock, int BrandId, int CategoryId, BrandDto Brand, CategoryDto Category, string Weight,
    string Stiffness, string BalancePoint, string? ImageUrl, bool IsActive, DateTime CreatedAt)
{
    public IReadOnlyList<string> AdditionalImageUrls { get; init; } = [];
    public string? SkillLevel { get; init; }
}

public sealed record PageDto<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalItems)
{
    public int TotalPages => (int)Math.Ceiling((double)TotalItems / PageSize);
    [System.Text.Json.Serialization.JsonIgnore]
    public int TotalCount => TotalItems;
}

public sealed class ProductPageQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 12;
    public string? Search { get; init; }
    public int[] BrandId { get; init; } = [];
    public int? CategoryId { get; init; }
    public decimal? MinPrice { get; init; }
    public decimal? MaxPrice { get; init; }
    public string[] Weight { get; init; } = [];
    public string? Stiffness { get; init; }
    public string? BalancePoint { get; init; }
    public bool InStock { get; init; }
    public string? SortBy { get; init; }
    public string SortDir { get; init; } = "asc";
    public bool NewestFirst { get; init; }
}

public sealed class SaveProductRequest
{
    public string? SkillLevel { get; init; }
    public string Name { get; init; } = "";
    public string? Description { get; init; }
    public decimal Price { get; init; }
    public decimal? DiscountPrice { get; init; }
    public int Stock { get; init; }
    public int BrandId { get; init; }
    public int CategoryId { get; init; }
    public string Weight { get; init; } = "";
    public string Stiffness { get; init; } = "";
    public string BalancePoint { get; init; } = "";
    public string? ImageUrl { get; init; }
    public bool? IsActive { get; init; }
}

public sealed record SaveCategoryRequest(string Name);
public sealed record SaveBrandRequest(string Name, string? Logo);
