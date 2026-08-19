using Marketplace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Marketplace.Infrastructure.Persistence.Migrations;

/// <summary>
/// Retires a duplicate wood category after preserving every commercial reference
/// under the shared canonical category.
/// </summary>
[DbContext(typeof(MarketplaceDbContext))]
[Migration("20260819130000_ConsolidateWoodCategory")]
public partial class ConsolidateWoodCategory : Migration
{
    private const string Duplicate = "10000000-0000-0000-0000-000000000013";
    private const string Canonical = "10000000-0000-0000-0000-000000000006";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql($"""
            -- If a legacy need has both groups, preserve the canonical group and its line links.
            UPDATE "RfqItems" AS item
            SET "NeedCategoryId" = canonical."Id"
            FROM "ProcurementNeedCategories" AS duplicate
            INNER JOIN "ProcurementNeedCategories" AS canonical
                ON canonical."ProcurementNeedId" = duplicate."ProcurementNeedId"
               AND canonical."CategoryId" = '{Canonical}'::uuid
            WHERE duplicate."CategoryId" = '{Duplicate}'::uuid
              AND item."NeedCategoryId" = duplicate."Id";

            DELETE FROM "ProcurementNeedCategories" AS duplicate
            USING "ProcurementNeedCategories" AS canonical
            WHERE duplicate."ProcurementNeedId" = canonical."ProcurementNeedId"
              AND duplicate."CategoryId" = '{Duplicate}'::uuid
              AND canonical."CategoryId" = '{Canonical}'::uuid;

            UPDATE "ProcurementNeedCategories"
            SET "CategoryId" = '{Canonical}'::uuid
            WHERE "CategoryId" = '{Duplicate}'::uuid;

            INSERT INTO "SupplierCategories" ("SupplierId", "CategoryId")
            SELECT "SupplierId", '{Canonical}'::uuid FROM "SupplierCategories" WHERE "CategoryId" = '{Duplicate}'::uuid
            ON CONFLICT ("SupplierId", "CategoryId") DO NOTHING;
            DELETE FROM "SupplierCategories" WHERE "CategoryId" = '{Duplicate}'::uuid;

            INSERT INTO "SupplierCandidateCategories" ("CandidateId", "CategoryId")
            SELECT "CandidateId", '{Canonical}'::uuid FROM "SupplierCandidateCategories" WHERE "CategoryId" = '{Duplicate}'::uuid
            ON CONFLICT ("CandidateId", "CategoryId") DO NOTHING;
            DELETE FROM "SupplierCandidateCategories" WHERE "CategoryId" = '{Duplicate}'::uuid;

            INSERT INTO "ProjectTemplateCategories" ("ProjectTemplateId", "CategoryId")
            SELECT "ProjectTemplateId", '{Canonical}'::uuid FROM "ProjectTemplateCategories" WHERE "CategoryId" = '{Duplicate}'::uuid
            ON CONFLICT ("ProjectTemplateId", "CategoryId") DO NOTHING;
            DELETE FROM "ProjectTemplateCategories" WHERE "CategoryId" = '{Duplicate}'::uuid;

            UPDATE "AiIntakeSessions"
            SET "ExtractionJson" = REPLACE("ExtractionJson"::text, '{Duplicate}', '{Canonical}')::jsonb
            WHERE "ExtractionJson"::text LIKE '%{Duplicate}%';

            -- Candidates created before category consolidation did not receive an
            -- opportunity match for the canonical category. Backfill only live,
            -- same-county, exact-category matches; review remains explicit.
            INSERT INTO "OpportunityMatches"
                ("Id", "RfqId", "SupplierCandidateId", "Source", "Status", "MatchReason", "MatchedAt")
            SELECT
                md5('candidate-discovery:' || rfq."Id"::text || ':' || candidate."Id"::text)::uuid,
                rfq."Id",
                candidate."Id",
                2,
                1,
                'Categorie compatibilă: ' || category."Name" || ' · județ: ' || rfq."County",
                CURRENT_TIMESTAMP
            FROM "Rfqs" AS rfq
            INNER JOIN "ProcurementNeedCategories" AS needCategory
                ON needCategory."ProcurementNeedId" = rfq."Id"
            INNER JOIN "Categories" AS category ON category."Id" = needCategory."CategoryId"
            INNER JOIN "SupplierCandidates" AS candidate
                ON candidate."County" = rfq."County"
            INNER JOIN "SupplierCandidateCategories" AS candidateCategory
                ON candidateCategory."CandidateId" = candidate."Id"
               AND candidateCategory."CategoryId" = needCategory."CategoryId"
            WHERE rfq."Status" IN (1, 2)
              AND rfq."QuotationDeadline" > CURRENT_TIMESTAMP
              AND candidate."Status" NOT IN (2, 3)
              AND NOT EXISTS (
                  SELECT 1
                  FROM "OpportunityMatches" AS existing
                  WHERE existing."RfqId" = rfq."Id"
                    AND existing."SupplierCandidateId" = candidate."Id");

            DELETE FROM "Categories" WHERE "Id" = '{Duplicate}'::uuid;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Deliberately non-destructive: the duplicate category is retired rather than restored.
    }
}
