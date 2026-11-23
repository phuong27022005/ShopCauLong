using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace BadmintonShop.Infrastructure.Data.Migrations;

[DbContext(typeof(ShopDbContext))]
[Migration("20261113000002_UpdateHalbertec5000Details")]
public sealed class UpdateHalbertec5000Details : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE [Products]
            SET [Description] = N'Trình độ chơi: Trung bình
            Chiều dài vợt: 675 mm
            Phong cách chơi: Công thủ toàn diện
            Nội dung chơi: Cả đơn và đôi
            Trọng lượng: 3U (85–89 g), 4U (80–84 g)',
                [Stiffness] = N'Trung bình',
                [BalancePoint] = N'Hơi nặng đầu',
                [ImageUrl] = N'/product-images/li-ning-halbertec-5000/1.webp'
            WHERE [Slug] = N'li-ning-halbertec-5000';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Giữ dữ liệu sản phẩm đã cập nhật trong shop.
    }
}
