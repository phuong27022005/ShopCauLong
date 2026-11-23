using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace BadmintonShop.Infrastructure.Data.Migrations;

[DbContext(typeof(ShopDbContext))]
[Migration("20261112164059_UpdateArcsaber11Details")]
public sealed class UpdateArcsaber11Details : Migration
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
                [Stiffness] = N'Cứng',
                [BalancePoint] = N'Hơi nặng đầu',
                [ImageUrl] = N'/product-images/yonex-arcsaber-11/1.webp'
            WHERE [Slug] = N'yonex-arcsaber-11';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Không khôi phục dữ liệu mẫu vì sản phẩm có thể đã được chỉnh sửa trong shop.
    }
}
