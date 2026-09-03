using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Marketplace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSeapDocumentImportDossier : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DocumentGroup",
                table: "SeapTenderAttachments",
                type: "character varying(24)",
                maxLength: 24,
                nullable: false,
                defaultValue: "None");

            migrationBuilder.AddColumn<string>(
                name: "DownloadStatus",
                table: "SeapTenderAttachments",
                type: "character varying(24)",
                maxLength: 24,
                nullable: false,
                defaultValue: "None");

            migrationBuilder.AddColumn<string>(
                name: "Purpose",
                table: "SeapIngestionRuns",
                type: "character varying(24)",
                maxLength: 24,
                nullable: false,
                defaultValue: "DigestAndAnalyze");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DocumentGroup",
                table: "SeapTenderAttachments");

            migrationBuilder.DropColumn(
                name: "DownloadStatus",
                table: "SeapTenderAttachments");

            migrationBuilder.DropColumn(
                name: "Purpose",
                table: "SeapIngestionRuns");
        }
    }
}
