using Marketplace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Marketplace.Infrastructure.Persistence.Migrations;

[DbContext(typeof(MarketplaceDbContext))]
[Migration("20260724141500_AddProviderCoverageAndTotalOffers")]
public partial class AddProviderCoverageAndTotalOffers : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "CanFulfillMultiCategoryRequests",
            table: "SupplierDeliverySettings",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<int>(
            name: "PricingMode",
            table: "Offers",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<decimal>(
            name: "TotalPrice",
            table: "Offers",
            type: "numeric(18,2)",
            precision: 18,
            scale: 2,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "CanFulfillMultiCategoryRequests", table: "SupplierDeliverySettings");
        migrationBuilder.DropColumn(name: "PricingMode", table: "Offers");
        migrationBuilder.DropColumn(name: "TotalPrice", table: "Offers");
    }
}
