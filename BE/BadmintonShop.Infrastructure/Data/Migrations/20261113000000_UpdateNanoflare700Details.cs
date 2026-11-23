using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace BadmintonShop.Infrastructure.Data.Migrations;

[DbContext(typeof(ShopDbContext))]
[Migration("20261113000000_UpdateNanoflare700Details")]
public sealed class UpdateNanoflare700Details : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE [Products]
            SET [Description] = N'Cấu tạo khung: HM Graphite, NANOMETRIC DR, M40X, EX-HYPER MG
            Cấu tạo đũa: HM Graphite, SUPER HMG, ULTRA PE FIBER
            Khớp nối: NEW Built-in T-Joint
            Phiên bản trọng lượng: 5U và 4U
            Lực căng dây: 5U (19–27 lbs), 4U (20–28 lbs)
            Cỡ cán cầm: G5, G6',
                [Stiffness] = N'Trung bình',
                [BalancePoint] = N'Cân bằng',
                [ImageUrl] = N'/product-images/yonex-nanoflare-700/1.webp'
            WHERE [Slug] = N'yonex-nanoflare-700';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Giữ dữ liệu sản phẩm đã cập nhật trong shop.
    }
}
