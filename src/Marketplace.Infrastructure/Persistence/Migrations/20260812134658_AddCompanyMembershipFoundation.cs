using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Marketplace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyMembershipFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CompanyMemberships",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyMemberships", x => new { x.CompanyId, x.UserId });
                    table.ForeignKey(
                        name: "FK_CompanyMemberships_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CompanyMemberships_SupplierProfiles_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "SupplierProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyMemberships_CompanyId_UserId",
                table: "CompanyMemberships",
                columns: new[] { "CompanyId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompanyMemberships_UserId_RevokedAt",
                table: "CompanyMemberships",
                columns: new[] { "UserId", "RevokedAt" });

            // Expand → backfill → verify. Existing SupplierProfile IDs are also their
            // legacy owner user IDs, so this preserves all current provider data.
            migrationBuilder.Sql("""
                INSERT INTO "CompanyMemberships" ("CompanyId", "UserId", "Role", "CreatedAt", "UpdatedAt", "RevokedAt")
                SELECT "Id", "Id", 'Owner', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, NULL
                FROM "SupplierProfiles";
                """);
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM "SupplierProfiles" profile
                        LEFT JOIN "CompanyMemberships" membership
                            ON membership."CompanyId" = profile."Id"
                            AND membership."UserId" = profile."Id"
                            AND membership."Role" = 'Owner'
                            AND membership."RevokedAt" IS NULL
                        WHERE membership."CompanyId" IS NULL)
                    THEN
                        RAISE EXCEPTION 'Company membership owner backfill verification failed.';
                    END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompanyMemberships");
        }
    }
}
