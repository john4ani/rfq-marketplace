using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Marketplace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSeapTenders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SeapTenders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SeapExternalId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    CpvCodes = table.Column<string[]>(type: "text[]", nullable: false),
                    EstimatedValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    BuyerName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    PublicationDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Deadline = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeapTenders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SeapTenderAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenderId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    S3Url = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    DownloadedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeapTenderAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SeapTenderAttachments_SeapTenders_TenderId",
                        column: x => x.TenderId,
                        principalTable: "SeapTenders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SeapTenderAttachments_TenderId",
                table: "SeapTenderAttachments",
                column: "TenderId");

            migrationBuilder.CreateIndex(
                name: "IX_SeapTenders_SeapExternalId",
                table: "SeapTenders",
                column: "SeapExternalId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SeapTenderAttachments");

            migrationBuilder.DropTable(
                name: "SeapTenders");
        }
    }
}
