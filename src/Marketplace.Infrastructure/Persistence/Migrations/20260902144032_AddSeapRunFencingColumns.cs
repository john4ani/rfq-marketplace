using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Marketplace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSeapRunFencingColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AnalysisAttemptId",
                table: "SeapIngestionRuns",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AnalysisRequestHash",
                table: "SeapIngestionRuns",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ClaimVersion",
                table: "SeapIngestionRuns",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "ExtractedText",
                table: "SeapIngestionRuns",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AnalysisAttemptId",
                table: "SeapIngestionRuns");

            migrationBuilder.DropColumn(
                name: "AnalysisRequestHash",
                table: "SeapIngestionRuns");

            migrationBuilder.DropColumn(
                name: "ClaimVersion",
                table: "SeapIngestionRuns");

            migrationBuilder.DropColumn(
                name: "ExtractedText",
                table: "SeapIngestionRuns");
        }
    }
}
