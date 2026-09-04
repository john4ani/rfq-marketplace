using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Marketplace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSeapDocumentPreparation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DocumentRole",
                table: "SeapTenderAttachments",
                type: "character varying(24)",
                maxLength: 24,
                nullable: false,
                defaultValue: "Unknown");

            migrationBuilder.CreateTable(
                name: "SeapDocumentPreparations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenderId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttachmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceContentSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExtractionVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false, defaultValue: "Pending"),
                    HasTextLayer = table.Column<bool>(type: "boolean", nullable: false),
                    NeedsOcr = table.Column<bool>(type: "boolean", nullable: false),
                    PageCount = table.Column<int>(type: "integer", nullable: false),
                    CoveragePercent = table.Column<int>(type: "integer", nullable: true),
                    WarningCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastErrorCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    LastErrorMessage = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeapDocumentPreparations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SeapDocumentPreparations_SeapTenderAttachments_AttachmentId",
                        column: x => x.AttachmentId,
                        principalTable: "SeapTenderAttachments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SeapDocumentPreparations_SeapTenders_TenderId",
                        column: x => x.TenderId,
                        principalTable: "SeapTenders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SeapPreparedDocumentPages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PreparationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PageNumber = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    LetterCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    Warning = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeapPreparedDocumentPages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SeapPreparedDocumentPages_SeapDocumentPreparations_Preparat~",
                        column: x => x.PreparationId,
                        principalTable: "SeapDocumentPreparations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SeapPreparedDocumentSections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PreparationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    RawCode = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    NormalizedRole = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false),
                    Title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    StartPage = table.Column<int>(type: "integer", nullable: false),
                    EndPage = table.Column<int>(type: "integer", nullable: false),
                    LotIndex = table.Column<int>(type: "integer", nullable: true),
                    Text = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeapPreparedDocumentSections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SeapPreparedDocumentSections_SeapDocumentPreparations_Prepa~",
                        column: x => x.PreparationId,
                        principalTable: "SeapDocumentPreparations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SeapDocumentPreparations_AttachmentId",
                table: "SeapDocumentPreparations",
                column: "AttachmentId");

            migrationBuilder.CreateIndex(
                name: "IX_SeapDocumentPreparations_AttachmentId_SourceContentSha256_E~",
                table: "SeapDocumentPreparations",
                columns: new[] { "AttachmentId", "SourceContentSha256", "ExtractionVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SeapDocumentPreparations_TenderId",
                table: "SeapDocumentPreparations",
                column: "TenderId");

            migrationBuilder.CreateIndex(
                name: "IX_SeapPreparedDocumentPages_PreparationId_PageNumber",
                table: "SeapPreparedDocumentPages",
                columns: new[] { "PreparationId", "PageNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SeapPreparedDocumentSections_PreparationId_Ordinal",
                table: "SeapPreparedDocumentSections",
                columns: new[] { "PreparationId", "Ordinal" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SeapPreparedDocumentPages");

            migrationBuilder.DropTable(
                name: "SeapPreparedDocumentSections");

            migrationBuilder.DropTable(
                name: "SeapDocumentPreparations");

            migrationBuilder.DropColumn(
                name: "DocumentRole",
                table: "SeapTenderAttachments");
        }
    }
}
