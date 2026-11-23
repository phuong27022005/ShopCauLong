using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace BadmintonShop.Infrastructure.Data.Migrations;

[DbContext(typeof(ShopDbContext))]
[Migration("20261113000001_UpdateAxforce80Details")]
public sealed class UpdateAxforce80Details : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE [Products]
            SET [Description] = N'Chất liệu khung: Carbon Fibre (TB Nano + M40X + Superb Carbon)
            Điểm cân bằng: 300 ± 3 mm (Nặng đầu)
            Cỡ cán vợt: G5
            Sức căng tối đa: 30 lbs (13,6 kg)
            Màu sắc: Đỏ phối trắng',
                [Stiffness] = N'Trung bình',
                [BalancePoint] = N'Nặng đầu',
                [Weight] = N'4U',
                [ImageUrl] = N'/product-images/li-ning-axforce-80/1.webp'
            WHERE [Slug] = N'li-ning-axforce-80';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Giữ dữ liệu sản phẩm đã cập nhật trong shop.
    }
}
