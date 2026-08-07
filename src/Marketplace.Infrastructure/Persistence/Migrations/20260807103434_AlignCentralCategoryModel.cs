using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Marketplace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AlignCentralCategoryModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RfqItems_Categories_CategoryId",
                table: "RfqItems");

            migrationBuilder.DropForeignKey(
                name: "FK_Rfqs_Categories_PrimaryCategoryId",
                table: "Rfqs");

            migrationBuilder.DropForeignKey(
                name: "FK_SupplierCandidates_Categories_CategoryId",
                table: "SupplierCandidates");

            migrationBuilder.DropIndex(
                name: "IX_SupplierCandidates_CategoryId",
                table: "SupplierCandidates");

            migrationBuilder.DropIndex(
                name: "IX_SupplierCandidates_Status_County_CategoryId",
                table: "SupplierCandidates");

            migrationBuilder.DropIndex(
                name: "IX_Rfqs_PrimaryCategoryId",
                table: "Rfqs");

            migrationBuilder.DropIndex(
                name: "IX_RfqItems_CategoryId",
                table: "RfqItems");

            migrationBuilder.AddColumn<int>(
                name: "NeedType",
                table: "Rfqs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectTemplateId",
                table: "Rfqs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "NeedCategoryId",
                table: "RfqItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VerticalId",
                table: "Categories",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("20000000-0000-0000-0000-000000000001"));

            migrationBuilder.CreateTable(
                name: "ProcurementNeedCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcurementNeedId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcurementNeedCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcurementNeedCategories_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcurementNeedCategories_Rfqs_ProcurementNeedId",
                        column: x => x.ProcurementNeedId,
                        principalTable: "Rfqs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SupplierCandidateCategories",
                columns: table => new
                {
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierCandidateCategories", x => new { x.CandidateId, x.CategoryId });
                    table.ForeignKey(
                        name: "FK_SupplierCandidateCategories_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierCandidateCategories_SupplierCandidates_CandidateId",
                        column: x => x.CandidateId,
                        principalTable: "SupplierCandidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Verticals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Slug = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Verticals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProjectTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VerticalId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    Slug = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectTemplates_Verticals_VerticalId",
                        column: x => x.VerticalId,
                        principalTable: "Verticals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectTemplateCategories",
                columns: table => new
                {
                    ProjectTemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectTemplateCategories", x => new { x.ProjectTemplateId, x.CategoryId });
                    table.ForeignKey(
                        name: "FK_ProjectTemplateCategories_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectTemplateCategories_ProjectTemplates_ProjectTemplateId",
                        column: x => x.ProjectTemplateId,
                        principalTable: "ProjectTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000001"),
                columns: new[] { "Name", "Slug", "VerticalId" },
                values: new object[] { "Construcții", "constructii", new Guid("20000000-0000-0000-0000-000000000001") });

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000002"),
                column: "VerticalId",
                value: new Guid("20000000-0000-0000-0000-000000000001"));

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000003"),
                column: "VerticalId",
                value: new Guid("20000000-0000-0000-0000-000000000001"));

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000004"),
                column: "VerticalId",
                value: new Guid("20000000-0000-0000-0000-000000000001"));

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000005"),
                column: "VerticalId",
                value: new Guid("20000000-0000-0000-0000-000000000001"));

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000006"),
                column: "VerticalId",
                value: new Guid("20000000-0000-0000-0000-000000000001"));

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000007"),
                column: "VerticalId",
                value: new Guid("20000000-0000-0000-0000-000000000001"));

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000008"),
                column: "VerticalId",
                value: new Guid("20000000-0000-0000-0000-000000000001"));

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000009"),
                column: "VerticalId",
                value: new Guid("20000000-0000-0000-0000-000000000001"));

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000010"),
                column: "VerticalId",
                value: new Guid("20000000-0000-0000-0000-000000000001"));

            migrationBuilder.InsertData(
                table: "Verticals",
                columns: new[] { "Id", "IsActive", "Name", "Slug" },
                values: new object[] { new Guid("20000000-0000-0000-0000-000000000001"), true, "Construcții", "constructii" });

            // Preserve the existing single-category data while moving to the central many-category model.
            migrationBuilder.Sql("""
                INSERT INTO "SupplierCandidateCategories" ("CandidateId", "CategoryId")
                SELECT "Id", "CategoryId" FROM "SupplierCandidates";

                WITH required_categories AS (
                    SELECT "Id" AS need_id, "PrimaryCategoryId" AS category_id FROM "Rfqs"
                    UNION
                    SELECT "RfqId" AS need_id, "CategoryId" AS category_id
                    FROM "RfqItems" WHERE "CategoryId" IS NOT NULL
                ), ordered_categories AS (
                    SELECT need_id, category_id,
                           ROW_NUMBER() OVER (PARTITION BY need_id ORDER BY category_id) - 1 AS sort_order
                    FROM required_categories
                )
                INSERT INTO "ProcurementNeedCategories" ("Id", "ProcurementNeedId", "CategoryId", "SortOrder")
                SELECT md5(need_id::text || ':' || category_id::text)::uuid,
                       need_id, category_id, sort_order
                FROM ordered_categories;

                UPDATE "RfqItems" AS item
                SET "NeedCategoryId" = category."Id"
                FROM "ProcurementNeedCategories" AS category
                INNER JOIN "Rfqs" AS need ON need."Id" = category."ProcurementNeedId"
                WHERE item."RfqId" = category."ProcurementNeedId"
                  AND category."CategoryId" = COALESCE(item."CategoryId", need."PrimaryCategoryId");

                UPDATE "Rfqs"
                SET "NeedType" = CASE "InputType" WHEN 2 THEN 1 WHEN 3 THEN 2 ELSE 0 END;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "NeedCategoryId",
                table: "RfqItems",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.DropColumn(name: "CategoryId", table: "SupplierCandidates");
            migrationBuilder.DropColumn(name: "PrimaryCategoryId", table: "Rfqs");
            migrationBuilder.DropColumn(name: "CategoryId", table: "RfqItems");
            migrationBuilder.DropColumn(name: "CanFulfillMultiCategoryRequests", table: "SupplierDeliverySettings");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierCandidates_Status_County",
                table: "SupplierCandidates",
                columns: new[] { "Status", "County" });

            migrationBuilder.CreateIndex(
                name: "IX_Rfqs_ProjectTemplateId",
                table: "Rfqs",
                column: "ProjectTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_RfqItems_NeedCategoryId",
                table: "RfqItems",
                column: "NeedCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_VerticalId",
                table: "Categories",
                column: "VerticalId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementNeedCategories_CategoryId",
                table: "ProcurementNeedCategories",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementNeedCategories_ProcurementNeedId_CategoryId",
                table: "ProcurementNeedCategories",
                columns: new[] { "ProcurementNeedId", "CategoryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementNeedCategories_ProcurementNeedId_SortOrder",
                table: "ProcurementNeedCategories",
                columns: new[] { "ProcurementNeedId", "SortOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectTemplateCategories_CategoryId",
                table: "ProjectTemplateCategories",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectTemplates_VerticalId_Slug",
                table: "ProjectTemplates",
                columns: new[] { "VerticalId", "Slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierCandidateCategories_CategoryId",
                table: "SupplierCandidateCategories",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Verticals_Slug",
                table: "Verticals",
                column: "Slug",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Categories_Verticals_VerticalId",
                table: "Categories",
                column: "VerticalId",
                principalTable: "Verticals",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RfqItems_ProcurementNeedCategories_NeedCategoryId",
                table: "RfqItems",
                column: "NeedCategoryId",
                principalTable: "ProcurementNeedCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Rfqs_ProjectTemplates_ProjectTemplateId",
                table: "Rfqs",
                column: "ProjectTemplateId",
                principalTable: "ProjectTemplates",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Categories_Verticals_VerticalId",
                table: "Categories");

            migrationBuilder.DropForeignKey(
                name: "FK_RfqItems_ProcurementNeedCategories_NeedCategoryId",
                table: "RfqItems");

            migrationBuilder.DropForeignKey(
                name: "FK_Rfqs_ProjectTemplates_ProjectTemplateId",
                table: "Rfqs");

            migrationBuilder.AddColumn<Guid>(name: "CategoryId", table: "SupplierCandidates", type: "uuid", nullable: true);
            migrationBuilder.AddColumn<Guid>(name: "PrimaryCategoryId", table: "Rfqs", type: "uuid", nullable: true);
            migrationBuilder.AddColumn<Guid>(name: "CategoryId", table: "RfqItems", type: "uuid", nullable: true);
            migrationBuilder.AddColumn<bool>(name: "CanFulfillMultiCategoryRequests", table: "SupplierDeliverySettings", type: "boolean", nullable: false, defaultValue: false);

            migrationBuilder.Sql("""
                UPDATE "SupplierCandidates" AS candidate
                SET "CategoryId" = category."CategoryId"
                FROM (
                    SELECT DISTINCT ON ("CandidateId") "CandidateId", "CategoryId"
                    FROM "SupplierCandidateCategories" ORDER BY "CandidateId", "CategoryId"
                ) AS category
                WHERE candidate."Id" = category."CandidateId";

                UPDATE "Rfqs" AS need
                SET "PrimaryCategoryId" = category."CategoryId"
                FROM (
                    SELECT DISTINCT ON ("ProcurementNeedId") "ProcurementNeedId", "CategoryId"
                    FROM "ProcurementNeedCategories" ORDER BY "ProcurementNeedId", "SortOrder"
                ) AS category
                WHERE need."Id" = category."ProcurementNeedId";

                UPDATE "RfqItems" AS item
                SET "CategoryId" = category."CategoryId"
                FROM "ProcurementNeedCategories" AS category
                WHERE item."NeedCategoryId" = category."Id";
                """);

            migrationBuilder.AlterColumn<Guid>(name: "CategoryId", table: "SupplierCandidates", type: "uuid", nullable: false, oldClrType: typeof(Guid), oldType: "uuid", oldNullable: true);
            migrationBuilder.AlterColumn<Guid>(name: "PrimaryCategoryId", table: "Rfqs", type: "uuid", nullable: false, oldClrType: typeof(Guid), oldType: "uuid", oldNullable: true);

            migrationBuilder.DropTable(
                name: "ProcurementNeedCategories");

            migrationBuilder.DropTable(
                name: "ProjectTemplateCategories");

            migrationBuilder.DropTable(
                name: "SupplierCandidateCategories");

            migrationBuilder.DropTable(
                name: "ProjectTemplates");

            migrationBuilder.DropTable(
                name: "Verticals");

            migrationBuilder.DropIndex(
                name: "IX_SupplierCandidates_Status_County",
                table: "SupplierCandidates");

            migrationBuilder.DropIndex(
                name: "IX_Rfqs_ProjectTemplateId",
                table: "Rfqs");

            migrationBuilder.DropIndex(
                name: "IX_RfqItems_NeedCategoryId",
                table: "RfqItems");

            migrationBuilder.DropIndex(
                name: "IX_Categories_VerticalId",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "NeedType",
                table: "Rfqs");

            migrationBuilder.DropColumn(
                name: "ProjectTemplateId",
                table: "Rfqs");

            migrationBuilder.DropColumn(
                name: "NeedCategoryId",
                table: "RfqItems");

            migrationBuilder.DropColumn(
                name: "VerticalId",
                table: "Categories");

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000001"),
                columns: new[] { "Name", "Slug" },
                values: new object[] { "Materiale de construcții", "materiale-constructii" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierCandidates_CategoryId",
                table: "SupplierCandidates",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierCandidates_Status_County_CategoryId",
                table: "SupplierCandidates",
                columns: new[] { "Status", "County", "CategoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_Rfqs_PrimaryCategoryId",
                table: "Rfqs",
                column: "PrimaryCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_RfqItems_CategoryId",
                table: "RfqItems",
                column: "CategoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_RfqItems_Categories_CategoryId",
                table: "RfqItems",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Rfqs_Categories_PrimaryCategoryId",
                table: "Rfqs",
                column: "PrimaryCategoryId",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SupplierCandidates_Categories_CategoryId",
                table: "SupplierCandidates",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
