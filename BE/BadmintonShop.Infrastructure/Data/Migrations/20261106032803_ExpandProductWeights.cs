using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BadmintonShop.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ExpandProductWeights : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Products_Weight",
                table: "Products");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Products_Weight",
                table: "Products",
                sql: "[Weight] IN ('2U', '3U', '4U', '5U', 'F')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Products_Weight",
                table: "Products");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Products_Weight",
                table: "Products",
                sql: "[Weight] IN ('3U', '4U', '5U')");
        }
    }
}
