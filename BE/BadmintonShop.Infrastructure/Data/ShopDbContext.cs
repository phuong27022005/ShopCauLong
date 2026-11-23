using BadmintonShop.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace BadmintonShop.Infrastructure.Data;

public sealed class ShopDbContext(DbContextOptions<ShopDbContext> options) : DbContext(options)
{
    public int? AuditActorId { get; set; }
    public string AuditActorName { get; set; } = "Hệ thống";
    public DbSet<AccountActionToken> AccountActionTokens => Set<AccountActionToken>();
    public DbSet<OutgoingEmail> OutgoingEmails => Set<OutgoingEmail>();
    public DbSet<Favorite> Favorites => Set<Favorite>();
    public DbSet<OrderHistory> OrderHistories => Set<OrderHistory>();
    public DbSet<RefreshSession> RefreshSessions => Set<RefreshSession>();
    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Coupon> Coupons => Set<Coupon>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        CaptureOrderHistory();
        foreach (var entry in ChangeTracker.Entries<Product>().Where(x => x.State is EntityState.Added or EntityState.Modified))
            entry.Entity.SearchName = SearchText.Normalize(entry.Entity.Name);
        foreach (var entry in ChangeTracker.Entries<Coupon>().Where(x => x.State is EntityState.Added or EntityState.Modified))
            entry.Entity.Code = entry.Entity.Code.Trim().ToUpperInvariant();
        PrepareVersions();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        CaptureOrderHistory();
        foreach (var entry in ChangeTracker.Entries<Product>().Where(x => x.State is EntityState.Added or EntityState.Modified))
            entry.Entity.SearchName = SearchText.Normalize(entry.Entity.Name);
        foreach (var entry in ChangeTracker.Entries<Coupon>().Where(x => x.State is EntityState.Added or EntityState.Modified))
            entry.Entity.Code = entry.Entity.Code.Trim().ToUpperInvariant();
        PrepareVersions();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    private void CaptureOrderHistory()
    {
        foreach (var entry in ChangeTracker.Entries<Order>().ToList())
        {
            foreach (var property in new[] { nameof(Order.Status), nameof(Order.PaymentStatus) })
            {
                var value = entry.Property<string>(property);
                if (entry.State != EntityState.Added && !(entry.State == EntityState.Modified && value.IsModified && value.OriginalValue != value.CurrentValue)) continue;
                if (entry.State == EntityState.Added && property == nameof(Order.PaymentStatus)) continue;
                OrderHistories.Add(new OrderHistory { Order = entry.Entity, ActorId = AuditActorId,
                    ActorName = AuditActorName, Event = property, FromValue = entry.State == EntityState.Added ? null : value.OriginalValue,
                    ToValue = value.CurrentValue, CreatedAt = DateTime.UtcNow });
            }
        }
    }

    private void PrepareVersions()
    {
        // SQLite dùng token do ứng dụng tạo để kiểm thử optimistic concurrency.
        if (Database.IsSqlServer()) return;
        foreach (var entry in ChangeTracker.Entries().Where(x => x.Entity is Product or Order or Coupon
            && x.State is EntityState.Added or EntityState.Modified))
            entry.Property("RowVersion").CurrentValue = Guid.NewGuid().ToByteArray();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AccountActionToken>(e =>
        {
            e.Property(x => x.Purpose).HasMaxLength(20);
            e.Property(x => x.TokenHash).HasMaxLength(64);
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
        });
        modelBuilder.Entity<OutgoingEmail>(e =>
        {
            e.Property(x => x.Recipient).HasMaxLength(254);
            e.Property(x => x.Subject).HasMaxLength(200);
            e.HasIndex(x => new { x.SentAt, x.NextAttemptAt });
        });
        modelBuilder.Entity<Favorite>(e =>
        {
            e.HasKey(x => new { x.UserId, x.ProductId });
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId);
            e.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId);
        });
        modelBuilder.Entity<OrderHistory>(e =>
        {
            e.Property(x => x.ActorName).HasMaxLength(150);
            e.Property(x => x.Event).HasMaxLength(30);
            e.Property(x => x.FromValue).HasMaxLength(30);
            e.Property(x => x.ToValue).HasMaxLength(30);
            e.HasIndex(x => new { x.OrderId, x.CreatedAt });
            e.HasOne(x => x.Order).WithMany().HasForeignKey(x => x.OrderId);
        });
        modelBuilder.Entity<Product>().Property(x => x.SkillLevel).HasMaxLength(100);
        modelBuilder.Entity<User>().Property(x => x.TokenVersion).IsConcurrencyToken();
        modelBuilder.Entity<RefreshSession>(e =>
        {
            e.Property(x => x.TokenHash).HasMaxLength(64);
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<User>(e =>
        {
            e.Property(x => x.FullName).HasMaxLength(150);
            e.Property(x => x.Email).HasMaxLength(254);
            e.Property(x => x.PasswordHash).HasMaxLength(512);
            e.Property(x => x.Phone).HasMaxLength(20);
            e.Property(x => x.Role).HasMaxLength(30);
            e.HasIndex(x => x.Email).IsUnique();
        });

        modelBuilder.Entity<Brand>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.Logo).HasMaxLength(2048);
        });

        modelBuilder.Entity<Category>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.Slug).HasMaxLength(150);
            e.HasIndex(x => x.Slug).IsUnique();
        });

        modelBuilder.Entity<Product>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.SearchName).HasMaxLength(400);
            e.HasIndex(x => x.Price);
            e.HasIndex(x => x.Weight);
            e.Property(x => x.Slug).HasMaxLength(220);
            e.Property(x => x.Description).HasMaxLength(4000);
            e.Property(x => x.Weight).HasMaxLength(2);
            e.Property(x => x.Stiffness).HasMaxLength(50);
            e.Property(x => x.BalancePoint).HasMaxLength(50);
            e.Property(x => x.ImageUrl).HasMaxLength(2048);
            e.HasIndex(x => x.Slug).IsUnique();
            e.Property(x => x.Price).HasPrecision(18, 2);
            e.Property(x => x.DiscountPrice).HasPrecision(18, 2);
            e.ToTable(table => table.HasCheckConstraint("CK_Products_Weight", "[Weight] IN ('2U', '3U', '4U', '5U', 'F')"));
            e.HasOne(x => x.Brand).WithMany().HasForeignKey(x => x.BrandId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Cart>(e =>
        {
            e.HasOne<User>().WithOne().HasForeignKey<Cart>(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CartItem>(e =>
        {
            e.HasIndex(x => new { x.CartId, x.ProductId }).IsUnique();
            e.HasOne<Cart>().WithMany().HasForeignKey(x => x.CartId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Order>(e =>
        {
            e.Property(x => x.Status).HasMaxLength(30);
            e.Property(x => x.ShippingAddress).HasMaxLength(500);
            e.Property(x => x.Phone).HasMaxLength(20);
            e.Property(x => x.PaymentMethod).HasMaxLength(30);
            e.Property(x => x.TotalAmount).HasPrecision(18, 2);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<OrderItem>(e =>
        {
            e.Property(x => x.UnitPrice).HasPrecision(18, 2);
            e.HasOne<Order>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        });

        foreach (var entity in new[] { typeof(Product), typeof(Order), typeof(Coupon) })
        {
            var version = modelBuilder.Entity(entity).Property<byte[]>("RowVersion").IsConcurrencyToken();
            if (Database.IsSqlServer()) version.IsRowVersion();
            else version.ValueGeneratedNever();
        }
        modelBuilder.Entity<Order>().Property(x => x.Code).HasMaxLength(40);
        modelBuilder.Entity<Order>().HasIndex(x => x.Code).IsUnique().HasFilter("[Code] IS NOT NULL");
        modelBuilder.Entity<Order>().Property(x => x.FullName).HasMaxLength(150);
        modelBuilder.Entity<Order>().Property(x => x.Note).HasMaxLength(1000);
        modelBuilder.Entity<Order>().HasIndex(x => new { x.Status, x.CreatedAt });
        modelBuilder.Entity<OrderItem>().Property(x => x.ProductName).HasMaxLength(200);
        modelBuilder.Entity<Order>().Property(x => x.PaymentStatus).HasMaxLength(20).HasDefaultValue("Unpaid");
        modelBuilder.Entity<Order>().HasIndex(x => new { x.PaymentMethod, x.PaymentStatus, x.PaymentExpiredAt });
        modelBuilder.Entity<PaymentTransaction>(e =>
        {
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.Property(x => x.VnpTxnRef).HasMaxLength(100);
            e.HasIndex(x => x.VnpTxnRef).IsUnique();
            e.Property(x => x.VnpTransactionNo).HasMaxLength(100);
            e.Property(x => x.ResponseCode).HasMaxLength(2);
            e.HasOne<Order>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<Review>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.ProductId }).IsUnique();
            e.HasIndex(x => new { x.ProductId, x.CreatedAt });
            e.Property(x => x.Comment).HasMaxLength(2000);
            e.ToTable(t => t.HasCheckConstraint("CK_Review_Rating", "[Rating] BETWEEN 1 AND 5"));
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<Coupon>(e =>
        {
            e.Property(x => x.Code).HasMaxLength(50);
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Type).HasMaxLength(10);
            e.Property(x => x.Value).HasPrecision(18,2);
            e.Property(x => x.MinOrderAmount).HasPrecision(18,2);
            e.Property(x => x.MaxDiscount).HasPrecision(18,2);
            e.ToTable(t => t.HasCheckConstraint("CK_Coupon_Usage", "[UsedCount] >= 0 AND [UsedCount] <= [UsageLimit]"));
        });
        modelBuilder.Entity<Order>().Property(x => x.CouponCode).HasMaxLength(50);
        modelBuilder.Entity<Order>().Property(x => x.DiscountAmount).HasPrecision(18,2);
        modelBuilder.Entity<Order>().HasOne<Coupon>().WithMany().HasForeignKey(x => x.CouponId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Order>().HasIndex(x => x.CreatedAt);
        modelBuilder.Entity<User>().HasIndex(x => new { x.Role, x.CreatedAt });
        modelBuilder.Entity<Product>().HasIndex(x => new { x.IsActive, x.Stock });
        ShopSeedData.Configure(modelBuilder);
    }
}
