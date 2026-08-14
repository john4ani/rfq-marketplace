using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Marketplace.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class BackfillMissingLegacyCompanyMemberships : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Some deployed legacy SupplierProfiles predate the membership foundation.
        // Their shared primary key is the legacy owner user ID, so this additive,
        // idempotent repair preserves company, offer and trust ownership exactly.
        migrationBuilder.Sql("""
            INSERT INTO "CompanyMemberships" ("CompanyId", "UserId", "Role", "CreatedAt", "UpdatedAt", "RevokedAt")
            SELECT profile."Id", profile."Id", 'Owner', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, NULL
            FROM "SupplierProfiles" profile
            WHERE NOT EXISTS (
                SELECT 1
                FROM "CompanyMemberships" membership
                WHERE membership."CompanyId" = profile."Id"
                  AND membership."UserId" = profile."Id"
            )
            ON CONFLICT ("CompanyId", "UserId") DO NOTHING;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Deliberately non-destructive: these memberships restore legacy ownership.
    }
}
