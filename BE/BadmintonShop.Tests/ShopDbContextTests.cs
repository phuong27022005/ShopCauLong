using BadmintonShop.Core.Entities;
using BadmintonShop.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BadmintonShop.Tests;

public sealed class ShopDbContextTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:;Foreign Keys=True");
    private DbContextOptions<ShopDbContext> _options = null!;

    public async Task InitializeAsync()
    {
        // Giữ kết nối mở để các DbContext trong cùng test dùng chung dữ liệu.
        await _connection.OpenAsync();
        _options = new DbContextOptionsBuilder<ShopDbContext>().UseSqlite(_connection).Options;
        await using var db = new ShopDbContext(_options);
        await db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public async Task DatabaseContainsExpectedSeedData()
    {
        await using var db = new ShopDbContext(_options);

        Assert.Equal(new[] { "Admin", "Customer" },
            await db.Users.OrderBy(user => user.Id).Select(user => user.Role).ToArrayAsync());
        Assert.Equal(new[] { "Yonex", "Li-Ning", "Victor", "Mizuno", "Lining" },
            await db.Brands.OrderBy(brand => brand.Id).Select(brand => brand.Name).ToArrayAsync());
        Assert.Equal(3, await db.Categories.CountAsync());
        Assert.Equal(15, await db.Products.CountAsync());
    }

    [Fact]
    public async Task DuplicateUserEmailIsRejected()
    {
        await using var db = new ShopDbContext(_options);
        var email = await db.Users.Where(user => user.Id == 1).Select(user => user.Email).SingleAsync();
        db.Users.Add(new User
        {
            FullName = "Tài khoản trùng",
            Email = email,
            PasswordHash = "!",
            Role = "Customer",
            CreatedAt = DateTime.UtcNow
        });

        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());

        Assert.Equal(2067, Assert.IsType<SqliteException>(error.InnerException).SqliteExtendedErrorCode);
    }

    [Fact]
    public async Task BrandWithProductsCannotBeDeleted()
    {
        await using var db = new ShopDbContext(_options);
        var brand = await db.Brands.SingleAsync(brand => brand.Id == 1);
        db.Brands.Remove(brand);

        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());

        // SQLite báo RESTRICT qua trigger hoặc qua mã lỗi khóa ngoại.
        Assert.Contains(Assert.IsType<SqliteException>(error.InnerException).SqliteExtendedErrorCode,
            new[] { 1811, 787 });
        await using var read = new ShopDbContext(_options);
        Assert.True(await read.Brands.AnyAsync(brand => brand.Id == 1));
        Assert.True(await read.Products.AnyAsync(product => product.BrandId == 1));
    }

    [Fact]
    public async Task OrderItemRetainsPurchasePriceAfterProductPriceChanges()
    {
        const decimal purchasePrice = 899_000.50m;
        await using var db = new ShopDbContext(_options);
        var product = await db.Products.SingleAsync(product => product.Id == 1);
        product.DiscountPrice = purchasePrice;
        var order = new Order
        {
            UserId = 2,
            TotalAmount = purchasePrice * 2,
            Status = "Pending",
            ShippingAddress = "Hà Nội",
            Phone = "0900000000",
            PaymentMethod = "COD",
            CreatedAt = DateTime.UtcNow
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        db.OrderItems.Add(new OrderItem
        {
            OrderId = order.Id,
            ProductId = product.Id,
            Quantity = 2,
            UnitPrice = product.DiscountPrice ?? product.Price
        });
        await db.SaveChangesAsync();

        product.Price = 1_050_000m;
        product.DiscountPrice = null;
        await db.SaveChangesAsync();

        await using var read = new ShopDbContext(_options);
        var item = await read.OrderItems.SingleAsync(item => item.OrderId == order.Id);
        Assert.Equal(purchasePrice, item.UnitPrice);
        Assert.Equal(1_050_000m, (await read.Products.FindAsync(product.Id))!.Price);
        Assert.Equal(purchasePrice * 2, item.UnitPrice * item.Quantity);
    }
}
