namespace BadmintonShop.Core.Entities;

public sealed class User
{
    public bool EmailConfirmed { get; set; }
    public int TokenVersion { get; set; }
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string Role { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
