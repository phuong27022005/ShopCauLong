using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BadmintonShop.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Astrox88DPlayDraft : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AdditionalImageUrls",
                table: "Products",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.Sql("""
                INSERT INTO [Products] (
                    [Name], [SearchName], [Slug], [Description], [Price], [Stock], [BrandId], [CategoryId],
                    [Weight], [Stiffness], [BalancePoint], [ImageUrl], [AdditionalImageUrls], [IsActive], [CreatedAt])
                SELECT
                    N'Yonex Astrox 88D Play 2024', N'yonex astrox 88d play 2024', N'yonex-astrox-88d-play-2024',
                    N'Trình độ chơi: Mới chơi
                Nội dung chơi: Cả đơn và đôi
                Phong cách chơi: Tấn công
                Trọng lượng: 4U (80–84 g)
                Chiều dài vợt: 675 mm
                Swingweight: 84–86 kg/cm2
                Chiều dài cán vợt: 200 mm',
                    0, 0, 1, 1, N'4U', N'Trung bình', N'Hơi nặng đầu',
                    N'/product-images/yonex-astrox-88d-play/3.webp',
                    N'["/product-images/yonex-astrox-88d-play/2.webp","/product-images/yonex-astrox-88d-play/1.webp"]',
                    0, SYSUTCDATETIME()
                WHERE NOT EXISTS (SELECT 1 FROM [Products] WHERE [Slug] = N'yonex-astrox-88d-play-2024');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Giữ sản phẩm vì quản trị viên có thể đã chỉnh sửa hoặc bán sau migration.
            migrationBuilder.DropColumn(
                name: "AdditionalImageUrls",
                table: "Products");
        }
    }
}
