using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Marketplace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSeapDocumentImportStage1Hardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Split the legacy single S3Url column (which held EITHER the remote e-licitatie.ro URL OR a local
            // storage reference) into the honest SourceUrl/StorageReference pair WITHOUT losing data:
            //   • rows whose S3Url started with http → SourceUrl (remote SEAP provenance);
            //   • all other rows (local:// / s3:// object references) → StorageReference.
            migrationBuilder.AddColumn<string>(
                name: "SourceUrl",
                table: "SeapTenderAttachments",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StorageReference",
                table: "SeapTenderAttachments",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "SeapTenderAttachments"
                SET "SourceUrl" = "S3Url"
                WHERE "S3Url" LIKE 'http%';

                UPDATE "SeapTenderAttachments"
                SET "StorageReference" = "S3Url"
                WHERE "S3Url" NOT LIKE 'http%';
                """);

            migrationBuilder.DropColumn(
                name: "S3Url",
                table: "SeapTenderAttachments");

            migrationBuilder.AddColumn<string>(
                name: "SourceSection",
                table: "SeapTenderAttachments",
                type: "character varying(96)",
                maxLength: 96,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DownloadErrorCode",
                table: "SeapTenderAttachments",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            // Defensive pre-cleanup: legacy code only de-duplicated by file name, so an environment that ever
            // raced two document fetches may hold duplicate (TenderId, SourceDocumentId) rows. Keep the newest
            // row per upstream document so the unique index below can always be created.
            migrationBuilder.Sql(
                """
                DELETE FROM "SeapTenderAttachments" AS older
                USING "SeapTenderAttachments" AS newer
                WHERE older."TenderId" = newer."TenderId"
                  AND older."SourceDocumentId" IS NOT NULL
                  AND older."SourceDocumentId" = newer."SourceDocumentId"
                  AND older."Id" < newer."Id";
                """);

            // DB-level idempotency for the durable document list: one upstream document per tender, even when
            // two resolve/fetch executions race. PostgreSQL/SQL Server both allow several NULL SourceDocumentId
            // rows (legacy browsing copies that never resolved an upstream code).
            migrationBuilder.CreateIndex(
                name: "IX_SeapTenderAttachments_TenderId_SourceDocumentId",
                table: "SeapTenderAttachments",
                columns: new[] { "TenderId", "SourceDocumentId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SeapTenderAttachments_TenderId_SourceDocumentId",
                table: "SeapTenderAttachments");

            migrationBuilder.DropColumn(
                name: "DownloadErrorCode",
                table: "SeapTenderAttachments");

            migrationBuilder.DropColumn(
                name: "SourceSection",
                table: "SeapTenderAttachments");

            // Fold the two columns back into the legacy single S3Url column: SourceUrl wins (the remote value is
            // the higher-fidelity source), StorageReference fills rows that only had a local copy.
            migrationBuilder.AddColumn<string>(
                name: "S3Url",
                table: "SeapTenderAttachments",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                """
                UPDATE "SeapTenderAttachments"
                SET "S3Url" = COALESCE("SourceUrl", "StorageReference", '')
                """);

            migrationBuilder.DropColumn(
                name: "SourceUrl",
                table: "SeapTenderAttachments");

            migrationBuilder.DropColumn(
                name: "StorageReference",
                table: "SeapTenderAttachments");
        }
    }
}
