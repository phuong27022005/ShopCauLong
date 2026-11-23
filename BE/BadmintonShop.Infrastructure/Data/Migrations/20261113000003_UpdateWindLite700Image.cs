using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace BadmintonShop.Infrastructure.Data.Migrations;

[DbContext(typeof(ShopDbContext))]
[Migration("20261113000003_UpdateWindLite700Image")]
public sealed class UpdateWindLite700Image : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE [Products]
            SET [ImageUrl] = N'/product-images/li-ning-wind-lite-700/1.webp'
            WHERE [Slug] = N'li-ning-wind-lite-700';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Giữ ảnh hiện tại vì sản phẩm có thể đã được chỉnh sửa trong shop.
    }
}
