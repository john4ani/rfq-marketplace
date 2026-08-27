using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Marketplace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSeapEnrichmentFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CategoryId",
                table: "SeapTenders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "County",
                table: "SeapTenders",
                type: "character varying(48)",
                maxLength: 48,
                nullable: true);

            migrationBuilder.AddColumn<string[]>(
                name: "CpvNames",
                table: "SeapTenders",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);

            migrationBuilder.CreateTable(
                name: "CpvCategoryMappings",
                columns: table => new
                {
                    CpvCode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CpvCategoryMappings", x => x.CpvCode);
                    table.ForeignKey(
                        name: "FK_CpvCategoryMappings_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SeapTenders_CategoryId",
                table: "SeapTenders",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_SeapTenders_County",
                table: "SeapTenders",
                column: "County");

            migrationBuilder.CreateIndex(
                name: "IX_CpvCategoryMappings_CategoryId",
                table: "CpvCategoryMappings",
                column: "CategoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_SeapTenders_Categories_CategoryId",
                table: "SeapTenders",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SeapTenders_Categories_CategoryId",
                table: "SeapTenders");

            migrationBuilder.DropTable(
                name: "CpvCategoryMappings");

            migrationBuilder.DropIndex(
                name: "IX_SeapTenders_CategoryId",
                table: "SeapTenders");

            migrationBuilder.DropIndex(
                name: "IX_SeapTenders_County",
                table: "SeapTenders");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "SeapTenders");

            migrationBuilder.DropColumn(
                name: "County",
                table: "SeapTenders");

            migrationBuilder.DropColumn(
                name: "CpvNames",
                table: "SeapTenders");
        }
    }
}
