using Marketplace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Marketplace.Infrastructure.Persistence.Migrations;

/// <summary>
/// Restores discovery matches for existing candidates after their categories
/// were consolidated into the shared commercial catalog.
/// </summary>
[DbContext(typeof(MarketplaceDbContext))]
[Migration("20260819133000_BackfillCandidateOpportunityMatches")]
public partial class BackfillCandidateOpportunityMatches : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
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
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Historical match records are intentionally retained.
    }
}
