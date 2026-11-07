using BadmintonShop.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace BadmintonShop.Infrastructure.Data;

internal static class ShopSeedData
{
    // Giữ ngày cố định để snapshot migration không đổi giữa các lần build.
    private static readonly DateTime SeedDate = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public static void Configure(ModelBuilder modelBuilder)
    {
        // Tài khoản mẫu cần được cấp mật khẩu trước khi đăng nhập.
        modelBuilder.Entity<User>().HasData(
            new User { Id = 1, FullName = "Quản trị viên", Email = "admin@example.invalid", PasswordHash = "AQAAAAIAAYagAAAAEEiJTaXqhna0OxReDMS6hXn3yV30Aq9inJc+rclx0BLuw6dff47B7ET+/8/uoOiIbw==", Role = "Admin", CreatedAt = SeedDate },
            new User { Id = 2, FullName = "Khách hàng mẫu", Email = "customer@example.invalid", PasswordHash = "AQAAAAIAAYagAAAAEBdOYCUaEVXi0TR1I7h6FS7oMycFL/ns8Lr7YHmwAq5BV328UyYnkMgJEPvw/9isoQ==", Role = "Customer", CreatedAt = SeedDate });

        modelBuilder.Entity<Brand>().HasData(
            new Brand { Id = 1, Name = "Yonex" },
            new Brand { Id = 2, Name = "Li-Ning" },
            new Brand { Id = 3, Name = "Victor" },
            new Brand { Id = 4, Name = "Mizuno" },
            new Brand { Id = 5, Name = "Lining" });

        modelBuilder.Entity<Category>().HasData(
            new Category { Id = 1, Name = "Vợt tấn công", Slug = "vot-tan-cong" },
            new Category { Id = 2, Name = "Vợt cân bằng", Slug = "vot-can-bang" },
            new Category { Id = 3, Name = "Vợt phòng thủ", Slug = "vot-phong-thu" });

        modelBuilder.Entity<Product>().HasData(
            Sample(1, "Yonex Astrox 88D", "yonex-astrox-88d", 1, 1, 950000m),
            Sample(2, "Yonex Arcsaber 11", "yonex-arcsaber-11", 1, 2, 1090000m),
            Sample(3, "Yonex Nanoflare 700", "yonex-nanoflare-700", 1, 3, 1230000m),
            Sample(4, "Li-Ning Axforce 80", "li-ning-axforce-80", 2, 1, 1370000m),
            Sample(5, "Li-Ning Halbertec 5000", "li-ning-halbertec-5000", 2, 2, 1510000m),
            Sample(6, "Li-Ning Wind Lite 700", "li-ning-wind-lite-700", 2, 3, 1650000m),
            Sample(7, "Victor Thruster K", "victor-thruster-k", 3, 1, 1790000m),
            Sample(8, "Victor DriveX 9X", "victor-drivex-9x", 3, 2, 1930000m),
            Sample(9, "Victor Auraspeed 90K", "victor-auraspeed-90k", 3, 3, 2070000m),
            Sample(10, "Mizuno Fortius 11", "mizuno-fortius-11", 4, 1, 2210000m),
            Sample(11, "Mizuno Acrospeed 7", "mizuno-acrospeed-7", 4, 2, 2350000m),
            Sample(12, "Mizuno Altius 03", "mizuno-altius-03", 4, 3, 2490000m),
            Sample(13, "Lining Turbo X", "lining-turbo-x", 5, 1, 2630000m),
            Sample(14, "Lining Allround X", "lining-allround-x", 5, 2, 2770000m),
            Sample(15, "Lining Feather X", "lining-feather-x", 5, 3, 2910000m));
    }

    private static Product Sample(int id, string name, string slug, int brandId, int categoryId, decimal price) =>
        new()
        {
            Id = id,
            Name = name,
            SearchName = SearchText.Normalize(name),
            Slug = slug,
            Price = price,
            BrandId = brandId,
            CategoryId = categoryId,
            Weight = categoryId switch { 1 => "3U", 2 => "4U", _ => "5U" },
            BalancePoint = categoryId switch { 1 => "Nặng đầu", 2 => "Cân bằng", _ => "Nhẹ đầu" },
            Stiffness = categoryId == 1 ? "Cứng" : "Trung bình",
            Stock = 10,
            IsActive = true,
            CreatedAt = SeedDate
        };
}
