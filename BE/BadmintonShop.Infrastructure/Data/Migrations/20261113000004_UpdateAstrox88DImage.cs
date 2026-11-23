using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace BadmintonShop.Infrastructure.Data.Migrations;

[DbContext(typeof(ShopDbContext))]
[Migration("20261113000004_UpdateAstrox88DImage")]
public sealed class UpdateAstrox88DImage : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE [Products]
            SET [ImageUrl] = N'/product-images/yonex-astrox-88d/88d-pro.png'
            WHERE [Slug] = N'yonex-astrox-88d';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Giữ ảnh hiện tại vì sản phẩm có thể đã được chỉnh sửa trong shop.
    }
}
