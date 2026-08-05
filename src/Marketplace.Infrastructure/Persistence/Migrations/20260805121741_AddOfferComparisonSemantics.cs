using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Marketplace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOfferComparisonSemantics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Coverage",
                table: "Offers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DeliveryTerms",
                table: "Offers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TaxTreatment",
                table: "Offers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "VatRate",
                table: "Offers",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAlternative",
                table: "OfferItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("""
                UPDATE "Offers"
                SET "DeliveryTerms" = CASE
                    WHEN "DeliveryIncluded" = FALSE THEN 2
                    WHEN "PricingMode" = 0 THEN 1
                    ELSE 0
                END;

                UPDATE "Offers" AS offer
                SET "Coverage" = 1
                WHERE offer."PricingMode" = 0
                  AND EXISTS (
                      SELECT 1
                      FROM "RfqItems" AS requested
                      WHERE requested."RfqId" = offer."RfqId"
                        AND NOT EXISTS (
                            SELECT 1
                            FROM "OfferItems" AS quoted
                            WHERE quoted."OfferId" = offer."Id"
                              AND quoted."RfqItemId" = requested."Id"
                              AND quoted."Available" = TRUE
                              AND quoted."Quantity" >= requested."Quantity"));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Coverage",
                table: "Offers");

            migrationBuilder.DropColumn(
                name: "DeliveryTerms",
                table: "Offers");

            migrationBuilder.DropColumn(
                name: "TaxTreatment",
                table: "Offers");

            migrationBuilder.DropColumn(
                name: "VatRate",
                table: "Offers");

            migrationBuilder.DropColumn(
                name: "IsAlternative",
                table: "OfferItems");
        }
    }
}
