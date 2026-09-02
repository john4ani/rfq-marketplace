using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Marketplace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSeapDirectResolveIngestion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AnalysisVersion",
                table: "SeapTenders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AttemptCount",
                table: "SeapTenders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CompletedAt",
                table: "SeapTenders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DocumentSetHash",
                table: "SeapTenders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImportMode",
                table: "SeapTenders",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Standard");

            migrationBuilder.AddColumn<string>(
                name: "LastErrorCode",
                table: "SeapTenders",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastErrorMessage",
                table: "SeapTenders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedExternalId",
                table: "SeapTenders",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            // Backfill existing rows before the unique index is created (the column is NOT NULL, so every
            // pre-migration tender gets its normalized notice number now — an empty string would collide).
            migrationBuilder.Sql(
                "UPDATE \"SeapTenders\" SET \"NormalizedExternalId\" = UPPER(BTRIM(\"SeapExternalId\")) WHERE \"NormalizedExternalId\" = '';");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StartedAt",
                table: "SeapTenders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SeapIngestionRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenderId = table.Column<Guid>(type: "uuid", nullable: false),
                    NoticeNo = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    LastErrorCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    LastErrorMessage = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeapIngestionRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SeapIngestionRuns_SeapTenders_TenderId",
                        column: x => x.TenderId,
                        principalTable: "SeapTenders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SeapTenders_NormalizedExternalId",
                table: "SeapTenders",
                column: "NormalizedExternalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SeapIngestionRuns_TenderId",
                table: "SeapIngestionRuns",
                column: "TenderId",
                unique: true,
                filter: "\"Status\" IN ('Pending', 'FetchingDocuments', 'Analyzing')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SeapIngestionRuns");

            migrationBuilder.DropIndex(
                name: "IX_SeapTenders_NormalizedExternalId",
                table: "SeapTenders");

            migrationBuilder.DropColumn(
                name: "AnalysisVersion",
                table: "SeapTenders");

            migrationBuilder.DropColumn(
                name: "AttemptCount",
                table: "SeapTenders");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "SeapTenders");

            migrationBuilder.DropColumn(
                name: "DocumentSetHash",
                table: "SeapTenders");

            migrationBuilder.DropColumn(
                name: "ImportMode",
                table: "SeapTenders");

            migrationBuilder.DropColumn(
                name: "LastErrorCode",
                table: "SeapTenders");

            migrationBuilder.DropColumn(
                name: "LastErrorMessage",
                table: "SeapTenders");

            migrationBuilder.DropColumn(
                name: "NormalizedExternalId",
                table: "SeapTenders");

            migrationBuilder.DropColumn(
                name: "StartedAt",
                table: "SeapTenders");
        }
    }
}
