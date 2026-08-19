using Marketplace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Marketplace.Infrastructure.Persistence.Migrations;

/// <summary>
/// Consolidates the former multi-category routing groups to the category that the
/// application already exposed as primary: lowest SortOrder, then Id as a stable tie-breaker.
/// Item associations are repointed before secondary groups are removed.
/// </summary>
[DbContext(typeof(MarketplaceDbContext))]
[Migration("20260819120000_EnforceSingleNeedCategory")]
public partial class EnforceSingleNeedCategory : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            WITH primary_categories AS (
                SELECT DISTINCT ON ("ProcurementNeedId") "Id", "ProcurementNeedId"
                FROM "ProcurementNeedCategories"
                ORDER BY "ProcurementNeedId", "SortOrder", "Id"
            )
            UPDATE "RfqItems" AS item
            SET "NeedCategoryId" = primary_category."Id"
            FROM primary_categories AS primary_category
            WHERE item."RfqId" = primary_category."ProcurementNeedId"
              AND item."NeedCategoryId" <> primary_category."Id";

            WITH primary_categories AS (
                SELECT DISTINCT ON ("ProcurementNeedId") "Id", "ProcurementNeedId"
                FROM "ProcurementNeedCategories"
                ORDER BY "ProcurementNeedId", "SortOrder", "Id"
            )
            DELETE FROM "ProcurementNeedCategories" AS category
            USING primary_categories AS primary_category
            WHERE category."ProcurementNeedId" = primary_category."ProcurementNeedId"
              AND category."Id" <> primary_category."Id";

            UPDATE "ProcurementNeedCategories" SET "SortOrder" = 0;
            """);

        migrationBuilder.CreateIndex(
            name: "IX_ProcurementNeedCategories_ProcurementNeedId",
            table: "ProcurementNeedCategories",
            column: "ProcurementNeedId",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropIndex(
            name: "IX_ProcurementNeedCategories_ProcurementNeedId",
            table: "ProcurementNeedCategories");
}
