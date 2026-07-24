using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Marketplace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBuyerMarketRadius : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SupplierSearchRadiusKm",
                table: "Rfqs",
                type: "integer",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.AddColumn<int>(
                name: "MarketRadiusKm",
                table: "BuyerProfiles",
                type: "integer",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Rfqs_SupplierSearchRadiusKm",
                table: "Rfqs",
                sql: "\"SupplierSearchRadiusKm\" BETWEEN 1 AND 1000");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BuyerProfiles_MarketRadiusKm",
                table: "BuyerProfiles",
                sql: "\"MarketRadiusKm\" BETWEEN 1 AND 1000");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Rfqs_SupplierSearchRadiusKm",
                table: "Rfqs");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BuyerProfiles_MarketRadiusKm",
                table: "BuyerProfiles");

            migrationBuilder.DropColumn(
                name: "SupplierSearchRadiusKm",
                table: "Rfqs");

            migrationBuilder.DropColumn(
                name: "MarketRadiusKm",
                table: "BuyerProfiles");
        }
    }
}
