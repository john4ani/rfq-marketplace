using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Marketplace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRfqs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Rfqs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BuyerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ProjectName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    PrimaryCategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeliveryAddress = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    DeliveryLocality = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    County = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Latitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    Longitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    PreferredDeliveryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    QuotationDeadline = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AlternativeProductsAccepted = table.Column<bool>(type: "boolean", nullable: false),
                    EstimatedBudget = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    BuyerNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rfqs", x => x.Id);
                    table.CheckConstraint("CK_Rfqs_EstimatedBudget", "\"EstimatedBudget\" IS NULL OR \"EstimatedBudget\" >= 0");
                    table.ForeignKey(
                        name: "FK_Rfqs_BuyerProfiles_BuyerId",
                        column: x => x.BuyerId,
                        principalTable: "BuyerProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Rfqs_Categories_PrimaryCategoryId",
                        column: x => x.PrimaryCategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RfqItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RfqId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemNumber = table.Column<int>(type: "integer", nullable: false),
                    ProductName = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    ManufacturerOrBrand = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    Specification = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    Unit = table.Column<int>(type: "integer", nullable: false),
                    AlternativesAccepted = table.Column<bool>(type: "boolean", nullable: false),
                    BuyerNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RfqItems", x => x.Id);
                    table.CheckConstraint("CK_RfqItems_Quantity", "\"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_RfqItems_Rfqs_RfqId",
                        column: x => x.RfqId,
                        principalTable: "Rfqs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RfqItems_RfqId_ItemNumber",
                table: "RfqItems",
                columns: new[] { "RfqId", "ItemNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Rfqs_BuyerId_Status",
                table: "Rfqs",
                columns: new[] { "BuyerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Rfqs_PrimaryCategoryId",
                table: "Rfqs",
                column: "PrimaryCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Rfqs_Status_QuotationDeadline",
                table: "Rfqs",
                columns: new[] { "Status", "QuotationDeadline" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RfqItems");

            migrationBuilder.DropTable(
                name: "Rfqs");
        }
    }
}
