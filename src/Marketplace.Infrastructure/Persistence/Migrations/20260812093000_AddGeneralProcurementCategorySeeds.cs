using Marketplace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Marketplace.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(MarketplaceDbContext))]
    [Migration("20260812093000_AddGeneralProcurementCategorySeeds")]
    public partial class AddGeneralProcurementCategorySeeds : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SQL avoids EF's current-model seed mapping during upgrades from Task 3A.
            migrationBuilder.Sql("""
                INSERT INTO "Verticals" ("Id", "IsActive", "Name", "Slug")
                VALUES ('20000000-0000-0000-0000-000000000002', TRUE, 'Achiziții generale (provizoriu)', 'achizitii-generale-provizoriu')
                ON CONFLICT ("Id") DO NOTHING;

                INSERT INTO "Categories" ("Id", "IsActive", "Name", "ParentId", "Slug", "SortOrder", "VerticalId") VALUES
                ('10000000-0000-0000-0000-000000000011', TRUE, 'Achiziții generale', NULL, 'achizitii-generale', 0, '20000000-0000-0000-0000-000000000002'),
                ('10000000-0000-0000-0000-000000000012', TRUE, 'Piese auto', '10000000-0000-0000-0000-000000000011', 'piese-auto', 10, '20000000-0000-0000-0000-000000000002'),
                ('10000000-0000-0000-0000-000000000013', TRUE, 'Lemn și produse din lemn', '10000000-0000-0000-0000-000000000011', 'lemn-produse-din-lemn', 20, '20000000-0000-0000-0000-000000000002'),
                ('10000000-0000-0000-0000-000000000014', TRUE, 'Transport și logistică', '10000000-0000-0000-0000-000000000011', 'transport-logistica', 30, '20000000-0000-0000-0000-000000000002'),
                ('10000000-0000-0000-0000-000000000015', TRUE, 'Închiriere utilaje', '10000000-0000-0000-0000-000000000011', 'inchiriere-utilaje', 40, '20000000-0000-0000-0000-000000000002'),
                ('10000000-0000-0000-0000-000000000016', TRUE, 'Servicii specializate', '10000000-0000-0000-0000-000000000011', 'servicii-specializate', 50, '20000000-0000-0000-0000-000000000002')
                ON CONFLICT ("Id") DO NOTHING;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM "Categories" WHERE "Id" IN (
                    '10000000-0000-0000-0000-000000000012', '10000000-0000-0000-0000-000000000013',
                    '10000000-0000-0000-0000-000000000014', '10000000-0000-0000-0000-000000000015',
                    '10000000-0000-0000-0000-000000000016', '10000000-0000-0000-0000-000000000011');
                DELETE FROM "Verticals" WHERE "Id" = '20000000-0000-0000-0000-000000000002';
                """);
        }
    }
}
