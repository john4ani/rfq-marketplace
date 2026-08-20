using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Marketplace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CompleteProfileLocationFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Country",
                table: "SupplierProfiles",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "România");

            migrationBuilder.AddColumn<int>(
                name: "MarketRadiusKm",
                table: "SupplierProfiles",
                type: "integer",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.AddColumn<string>(
                name: "DefaultAddress",
                table: "BuyerProfiles",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefaultCountry",
                table: "BuyerProfiles",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "România");

            migrationBuilder.AddColumn<decimal>(
                name: "DefaultLatitude",
                table: "BuyerProfiles",
                type: "numeric(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DefaultLongitude",
                table: "BuyerProfiles",
                type: "numeric(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Country",
                table: "SupplierProfiles");

            migrationBuilder.DropColumn(
                name: "MarketRadiusKm",
                table: "SupplierProfiles");

            migrationBuilder.DropColumn(
                name: "DefaultAddress",
                table: "BuyerProfiles");

            migrationBuilder.DropColumn(
                name: "DefaultCountry",
                table: "BuyerProfiles");

            migrationBuilder.DropColumn(
                name: "DefaultLatitude",
                table: "BuyerProfiles");

            migrationBuilder.DropColumn(
                name: "DefaultLongitude",
                table: "BuyerProfiles");
        }
    }
}
