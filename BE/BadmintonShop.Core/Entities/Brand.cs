namespace BadmintonShop.Core.Entities;

public sealed class Brand
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Logo { get; set; }
}
