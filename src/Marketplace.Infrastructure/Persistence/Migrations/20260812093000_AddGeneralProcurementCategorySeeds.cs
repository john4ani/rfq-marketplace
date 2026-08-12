using System;
using Marketplace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Marketplace.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(MarketplaceDbContext))]
    [Migration("20260812093000_AddGeneralProcurementCategorySeeds")]
    /// <inheritdoc />
    public partial class AddGeneralProcurementCategorySeeds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Verticals",
                columns: new[] { "Id", "IsActive", "Name", "Slug" },
                values: new object[] { new Guid("20000000-0000-0000-0000-000000000002"), true, "Achiziții generale (provizoriu)", "achizitii-generale-provizoriu" });

            migrationBuilder.InsertData(
                table: "Categories",
                columns: new[] { "Id", "IsActive", "Name", "ParentId", "Slug", "SortOrder", "VerticalId" },
                values: new object[] { new Guid("10000000-0000-0000-0000-000000000011"), true, "Achiziții generale", null, "achizitii-generale", 0, new Guid("20000000-0000-0000-0000-000000000002") });

            migrationBuilder.InsertData(
                table: "Categories",
                columns: new[] { "Id", "IsActive", "Name", "ParentId", "Slug", "SortOrder", "VerticalId" },
                values: new object[,]
                {
                    { new Guid("10000000-0000-0000-0000-000000000012"), true, "Piese auto", new Guid("10000000-0000-0000-0000-000000000011"), "piese-auto", 10, new Guid("20000000-0000-0000-0000-000000000002") },
                    { new Guid("10000000-0000-0000-0000-000000000013"), true, "Lemn și produse din lemn", new Guid("10000000-0000-0000-0000-000000000011"), "lemn-produse-din-lemn", 20, new Guid("20000000-0000-0000-0000-000000000002") },
                    { new Guid("10000000-0000-0000-0000-000000000014"), true, "Transport și logistică", new Guid("10000000-0000-0000-0000-000000000011"), "transport-logistica", 30, new Guid("20000000-0000-0000-0000-000000000002") },
                    { new Guid("10000000-0000-0000-0000-000000000015"), true, "Închiriere utilaje", new Guid("10000000-0000-0000-0000-000000000011"), "inchiriere-utilaje", 40, new Guid("20000000-0000-0000-0000-000000000002") },
                    { new Guid("10000000-0000-0000-0000-000000000016"), true, "Servicii specializate", new Guid("10000000-0000-0000-0000-000000000011"), "servicii-specializate", 50, new Guid("20000000-0000-0000-0000-000000000002") }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData("Categories", "Id", new Guid("10000000-0000-0000-0000-000000000012"));
            migrationBuilder.DeleteData("Categories", "Id", new Guid("10000000-0000-0000-0000-000000000013"));
            migrationBuilder.DeleteData("Categories", "Id", new Guid("10000000-0000-0000-0000-000000000014"));
            migrationBuilder.DeleteData("Categories", "Id", new Guid("10000000-0000-0000-0000-000000000015"));
            migrationBuilder.DeleteData("Categories", "Id", new Guid("10000000-0000-0000-0000-000000000016"));
            migrationBuilder.DeleteData("Categories", "Id", new Guid("10000000-0000-0000-0000-000000000011"));
            migrationBuilder.DeleteData("Verticals", "Id", new Guid("20000000-0000-0000-0000-000000000002"));
        }
    }
}
