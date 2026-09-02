using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Marketplace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSeapIngestionRunCheckpoints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AnalyzedDocumentSetHash",
                table: "SeapTenders",
                type: "text",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "AttemptCount",
                table: "SeapIngestionRuns",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "ClaimedBy",
                table: "SeapIngestionRuns",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DocumentsFetchedAt",
                table: "SeapIngestionRuns",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastHeartbeatAt",
                table: "SeapIngestionRuns",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LeaseExpiresAt",
                table: "SeapIngestionRuns",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NextAttemptAt",
                table: "SeapIngestionRuns",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TextExtractedAt",
                table: "SeapIngestionRuns",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AnalyzedDocumentSetHash",
                table: "SeapTenders");

            migrationBuilder.DropColumn(
                name: "ClaimedBy",
                table: "SeapIngestionRuns");

            migrationBuilder.DropColumn(
                name: "DocumentsFetchedAt",
                table: "SeapIngestionRuns");

            migrationBuilder.DropColumn(
                name: "LastHeartbeatAt",
                table: "SeapIngestionRuns");

            migrationBuilder.DropColumn(
                name: "LeaseExpiresAt",
                table: "SeapIngestionRuns");

            migrationBuilder.DropColumn(
                name: "NextAttemptAt",
                table: "SeapIngestionRuns");

            migrationBuilder.DropColumn(
                name: "TextExtractedAt",
                table: "SeapIngestionRuns");

            migrationBuilder.AlterColumn<int>(
                name: "AttemptCount",
                table: "SeapIngestionRuns",
                type: "integer",
                nullable: false,
                defaultValue: 1,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 0);
        }
    }
}
