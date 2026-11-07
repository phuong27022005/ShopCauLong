namespace BadmintonShop.Core.Entities;

public sealed class Product
{
    public string SearchName { get; set; } = "";
    public byte[] RowVersion { get; set; } = [];
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public decimal? DiscountPrice { get; set; }
    public int Stock { get; set; }
    public int BrandId { get; set; }
    public int CategoryId { get; set; }
    public Brand Brand { get; set; } = null!;
    public Category Category { get; set; } = null!;
    public string Weight { get; set; } = string.Empty;
    public string Stiffness { get; set; } = string.Empty;
    public string BalancePoint { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}
