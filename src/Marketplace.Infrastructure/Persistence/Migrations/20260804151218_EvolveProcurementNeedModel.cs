using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Marketplace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EvolveProcurementNeedModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "InputType",
                table: "Rfqs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Outcome",
                table: "Rfqs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OutcomeReason",
                table: "Rfqs",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "OutcomeRecordedAt",
                table: "Rfqs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Source",
                table: "Rfqs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Kind",
                table: "RfqItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ProcurementNeedAuditEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcurementNeedId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    FromStatus = table.Column<int>(type: "integer", nullable: false),
                    ToStatus = table.Column<int>(type: "integer", nullable: false),
                    Details = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcurementNeedAuditEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcurementNeedAuditEvents_Rfqs_ProcurementNeedId",
                        column: x => x.ProcurementNeedId,
                        principalTable: "Rfqs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementNeedAuditEvents_ActorId",
                table: "ProcurementNeedAuditEvents",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementNeedAuditEvents_ProcurementNeedId_OccurredAt",
                table: "ProcurementNeedAuditEvents",
                columns: new[] { "ProcurementNeedId", "OccurredAt" });

            // Preserve traceability for requests created before the audit trail existed.
            // RfqStatus and ProcurementNeedStatus deliberately share the same numeric lifecycle order.
            migrationBuilder.Sql(
                """
                INSERT INTO "ProcurementNeedAuditEvents"
                    ("Id", "ProcurementNeedId", "ActorId", "Type", "FromStatus", "ToStatus", "Details", "OccurredAt")
                SELECT "Id", "Id", "BuyerId", 10, "Status", "Status",
                       'Imported from the existing request lifecycle.', "CreatedAt"
                FROM "Rfqs";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProcurementNeedAuditEvents");

            migrationBuilder.DropColumn(
                name: "InputType",
                table: "Rfqs");

            migrationBuilder.DropColumn(
                name: "Outcome",
                table: "Rfqs");

            migrationBuilder.DropColumn(
                name: "OutcomeReason",
                table: "Rfqs");

            migrationBuilder.DropColumn(
                name: "OutcomeRecordedAt",
                table: "Rfqs");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "Rfqs");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "RfqItems");
        }
    }
}
