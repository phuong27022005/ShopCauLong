using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace BadmintonShop.Infrastructure.Data.Migrations;

[DbContext(typeof(ShopDbContext))]
[Migration("20261112164100_Astrox88DGameDraft")]
public sealed class Astrox88DGameDraft : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            INSERT INTO [Products] (
                [Name], [SearchName], [Slug], [Description], [Price], [Stock], [BrandId], [CategoryId],
                [Weight], [Stiffness], [BalancePoint], [ImageUrl], [AdditionalImageUrls], [IsActive], [CreatedAt])
            SELECT
                N'Yonex Astrox 88D Game 2024', N'yonex astrox 88d game 2024', N'yonex-astrox-88d-game-2024',
                N'Trình độ chơi: Trung bình
            Phong cách chơi: Tấn công
            Nội dung chơi: Cả đơn và đôi
            Trọng lượng: 4U (80–84 g)
            Chiều dài vợt: 675 mm',
                0, 0, 1, 1, N'4U', N'Trung bình', N'Nặng đầu',
                N'/product-images/yonex-astrox-88d-game/1.webp',
                N'["/product-images/yonex-astrox-88d-game/2.webp",
                   "/product-images/yonex-astrox-88d-game/3.webp",
                   "/product-images/yonex-astrox-88d-game/4.webp",
                   "/product-images/yonex-astrox-88d-game/5.webp",
                   "/product-images/yonex-astrox-88d-game/6.webp",
                   "/product-images/yonex-astrox-88d-game/7.webp"]',
                0, SYSUTCDATETIME()
            WHERE NOT EXISTS (SELECT 1 FROM [Products] WHERE [Slug] = N'yonex-astrox-88d-game-2024');
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Giữ sản phẩm để bảo toàn các chỉnh sửa và đơn hàng phát sinh sau migration.
    }
}
