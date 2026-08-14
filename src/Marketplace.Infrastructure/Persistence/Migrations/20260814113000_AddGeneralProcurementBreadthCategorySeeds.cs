using Marketplace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Marketplace.Infrastructure.Persistence.Migrations;

[DbContext(typeof(MarketplaceDbContext))]
[Migration("20260814113000_AddGeneralProcurementBreadthCategorySeeds")]
public partial class AddGeneralProcurementBreadthCategorySeeds : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            INSERT INTO "Categories" ("Id", "IsActive", "Name", "ParentId", "Slug", "SortOrder", "VerticalId") VALUES
            ('10000000-0000-0000-0000-000000000017', TRUE, 'Materiale și consumabile', '10000000-0000-0000-0000-000000000011', 'materiale-consumabile', 60, '20000000-0000-0000-0000-000000000002'),
            ('10000000-0000-0000-0000-000000000018', TRUE, 'Echipamente', '10000000-0000-0000-0000-000000000011', 'echipamente', 70, '20000000-0000-0000-0000-000000000002'),
            ('10000000-0000-0000-0000-000000000019', TRUE, 'Combustibili și încălzire', '10000000-0000-0000-0000-000000000011', 'combustibili-incalzire', 80, '20000000-0000-0000-0000-000000000002')
            ON CONFLICT ("Id") DO NOTHING;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE FROM "Categories" WHERE "Id" IN (
                '10000000-0000-0000-0000-000000000017',
                '10000000-0000-0000-0000-000000000018',
                '10000000-0000-0000-0000-000000000019');
            """);
    }
}
