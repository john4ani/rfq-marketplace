using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Marketplace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierCategoriesAndDeliverySettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Slug = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Categories_Categories_ParentId",
                        column: x => x.ParentId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupplierDeliveryAreas",
                columns: table => new
                {
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    County = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierDeliveryAreas", x => new { x.SupplierId, x.County });
                    table.ForeignKey(
                        name: "FK_SupplierDeliveryAreas_SupplierProfiles_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "SupplierProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SupplierDeliverySettings",
                columns: table => new
                {
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    NormalDeliveryRadiusKm = table.Column<int>(type: "integer", nullable: false),
                    MaximumDeliveryRadiusKm = table.Column<int>(type: "integer", nullable: false),
                    MinimumOrderValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    NationwideDelivery = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierDeliverySettings", x => x.SupplierId);
                    table.CheckConstraint("CK_SupplierDeliverySettings_MaximumRadius", "\"MaximumDeliveryRadiusKm\" BETWEEN \"NormalDeliveryRadiusKm\" AND 1000");
                    table.CheckConstraint("CK_SupplierDeliverySettings_MinimumOrder", "\"MinimumOrderValue\" IS NULL OR \"MinimumOrderValue\" >= 0");
                    table.CheckConstraint("CK_SupplierDeliverySettings_NormalRadius", "\"NormalDeliveryRadiusKm\" BETWEEN 1 AND 1000");
                    table.ForeignKey(
                        name: "FK_SupplierDeliverySettings_SupplierProfiles_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "SupplierProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SupplierCategories",
                columns: table => new
                {
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierCategories", x => new { x.SupplierId, x.CategoryId });
                    table.ForeignKey(
                        name: "FK_SupplierCategories_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierCategories_SupplierProfiles_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "SupplierProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Categories",
                columns: new[] { "Id", "IsActive", "Name", "ParentId", "Slug", "SortOrder" },
                values: new object[,]
                {
                    { new Guid("10000000-0000-0000-0000-000000000001"), true, "Materiale de construcții", null, "materiale-constructii", 0 },
                    { new Guid("10000000-0000-0000-0000-000000000002"), true, "Izolații", new Guid("10000000-0000-0000-0000-000000000001"), "izolatii", 10 },
                    { new Guid("10000000-0000-0000-0000-000000000003"), true, "Ciment și mortare", new Guid("10000000-0000-0000-0000-000000000001"), "ciment-mortare", 20 },
                    { new Guid("10000000-0000-0000-0000-000000000004"), true, "Zidărie", new Guid("10000000-0000-0000-0000-000000000001"), "zidarie", 30 },
                    { new Guid("10000000-0000-0000-0000-000000000005"), true, "Acoperișuri", new Guid("10000000-0000-0000-0000-000000000001"), "acoperisuri", 40 },
                    { new Guid("10000000-0000-0000-0000-000000000006"), true, "Lemn și plăci", new Guid("10000000-0000-0000-0000-000000000001"), "lemn-placi", 50 },
                    { new Guid("10000000-0000-0000-0000-000000000007"), true, "Gips-carton", new Guid("10000000-0000-0000-0000-000000000001"), "gips-carton", 60 },
                    { new Guid("10000000-0000-0000-0000-000000000008"), true, "Finisaje", new Guid("10000000-0000-0000-0000-000000000001"), "finisaje", 70 },
                    { new Guid("10000000-0000-0000-0000-000000000009"), true, "Instalații sanitare", new Guid("10000000-0000-0000-0000-000000000001"), "instalatii-sanitare", 80 },
                    { new Guid("10000000-0000-0000-0000-000000000010"), true, "Instalații electrice", new Guid("10000000-0000-0000-0000-000000000001"), "instalatii-electrice", 90 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Categories_ParentId",
                table: "Categories",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Slug",
                table: "Categories",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierCategories_CategoryId",
                table: "SupplierCategories",
                column: "CategoryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SupplierCategories");

            migrationBuilder.DropTable(
                name: "SupplierDeliveryAreas");

            migrationBuilder.DropTable(
                name: "SupplierDeliverySettings");

            migrationBuilder.DropTable(
                name: "Categories");
        }
    }
}
