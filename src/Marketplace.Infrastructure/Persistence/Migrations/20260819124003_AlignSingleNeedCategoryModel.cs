using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Marketplace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AlignSingleNeedCategoryModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO "Categories" ("Id", "IsActive", "Name", "ParentId", "Slug", "SortOrder", "VerticalId")
                VALUES
                    ('10000000-0000-0000-0000-000000000017', TRUE, 'Materiale și consumabile', '10000000-0000-0000-0000-000000000011', 'materiale-consumabile', 60, '20000000-0000-0000-0000-000000000002'),
                    ('10000000-0000-0000-0000-000000000018', TRUE, 'Echipamente', '10000000-0000-0000-0000-000000000011', 'echipamente', 70, '20000000-0000-0000-0000-000000000002'),
                    ('10000000-0000-0000-0000-000000000019', TRUE, 'Combustibili și încălzire', '10000000-0000-0000-0000-000000000011', 'combustibili-incalzire', 80, '20000000-0000-0000-0000-000000000002')
                ON CONFLICT ("Id") DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000017"));

            migrationBuilder.DeleteData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000018"));

            migrationBuilder.DeleteData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000019"));
        }
    }
}
