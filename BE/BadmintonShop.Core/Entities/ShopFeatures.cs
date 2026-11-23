namespace BadmintonShop.Core.Entities;

public sealed class AccountActionToken
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public string Purpose { get; set; } = "";
    public string TokenHash { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
}

public sealed class OutgoingEmail
{
    public int Id { get; set; }
    public string Recipient { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? SentAt { get; set; }
    public int Attempts { get; set; }
    public DateTime NextAttemptAt { get; set; }
}

public sealed class Favorite
{
    public int UserId { get; set; }
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}

public sealed class OrderHistory
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public int? ActorId { get; set; }
    public string ActorName { get; set; } = "Hệ thống";
    public string Event { get; set; } = "";
    public string? FromValue { get; set; }
    public string ToValue { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
