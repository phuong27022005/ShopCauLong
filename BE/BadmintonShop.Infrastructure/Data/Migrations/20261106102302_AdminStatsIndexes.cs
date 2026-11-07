using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BadmintonShop.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AdminStatsIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Users_Role_CreatedAt",
                table: "Users",
                columns: new[] { "Role", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Products_IsActive_Stock",
                table: "Products",
                columns: new[] { "IsActive", "Stock" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_CreatedAt",
                table: "Orders",
                column: "CreatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_Role_CreatedAt",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Products_IsActive_Stock",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Orders_CreatedAt",
                table: "Orders");
        }
    }
}
