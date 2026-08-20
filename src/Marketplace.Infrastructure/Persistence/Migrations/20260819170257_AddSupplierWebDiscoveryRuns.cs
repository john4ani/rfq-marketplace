using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Marketplace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierWebDiscoveryRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SupplierWebDiscoveryRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RfqId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    EvidenceJson = table.Column<string>(type: "jsonb", nullable: false),
                    QualificationJson = table.Column<string>(type: "jsonb", nullable: true),
                    FailureMessage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    WebSearchCallCount = table.Column<int>(type: "integer", nullable: false),
                    DiscoveryLatencyMs = table.Column<long>(type: "bigint", nullable: true),
                    DiscoveryInputTokens = table.Column<int>(type: "integer", nullable: true),
                    DiscoveryOutputTokens = table.Column<int>(type: "integer", nullable: true),
                    QualificationLatencyMs = table.Column<long>(type: "bigint", nullable: true),
                    QualificationInputTokens = table.Column<int>(type: "integer", nullable: true),
                    QualificationOutputTokens = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierWebDiscoveryRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierWebDiscoveryRuns_AspNetUsers_StartedBy",
                        column: x => x.StartedBy,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierWebDiscoveryRuns_Rfqs_RfqId",
                        column: x => x.RfqId,
                        principalTable: "Rfqs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierWebDiscoveryRuns_RfqId_StartedAt",
                table: "SupplierWebDiscoveryRuns",
                columns: new[] { "RfqId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierWebDiscoveryRuns_StartedBy",
                table: "SupplierWebDiscoveryRuns",
                column: "StartedBy");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SupplierWebDiscoveryRuns");
        }
    }
}
