using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Marketplace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOpportunityMatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OpportunityMatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RfqId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierProfileId = table.Column<Guid>(type: "uuid", nullable: true),
                    SupplierCandidateId = table.Column<Guid>(type: "uuid", nullable: true),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    MatchReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    MatchedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AdminReviewedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    AdminReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AdminReviewNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpportunityMatches", x => x.Id);
                    table.CheckConstraint("CK_OpportunityMatches_ExactlyOneTarget", "(\"SupplierProfileId\" IS NOT NULL) <> (\"SupplierCandidateId\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_OpportunityMatches_AspNetUsers_AdminReviewedBy",
                        column: x => x.AdminReviewedBy,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OpportunityMatches_Rfqs_RfqId",
                        column: x => x.RfqId,
                        principalTable: "Rfqs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OpportunityMatches_SupplierCandidates_SupplierCandidateId",
                        column: x => x.SupplierCandidateId,
                        principalTable: "SupplierCandidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OpportunityMatches_SupplierProfiles_SupplierProfileId",
                        column: x => x.SupplierProfileId,
                        principalTable: "SupplierProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OpportunityMatches_AdminReviewedBy",
                table: "OpportunityMatches",
                column: "AdminReviewedBy");

            migrationBuilder.CreateIndex(
                name: "IX_OpportunityMatches_RfqId_Status",
                table: "OpportunityMatches",
                columns: new[] { "RfqId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_OpportunityMatches_RfqId_SupplierCandidateId",
                table: "OpportunityMatches",
                columns: new[] { "RfqId", "SupplierCandidateId" },
                unique: true,
                filter: "\"SupplierCandidateId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OpportunityMatches_RfqId_SupplierProfileId",
                table: "OpportunityMatches",
                columns: new[] { "RfqId", "SupplierProfileId" },
                unique: true,
                filter: "\"SupplierProfileId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OpportunityMatches_SupplierCandidateId_Status_MatchedAt",
                table: "OpportunityMatches",
                columns: new[] { "SupplierCandidateId", "Status", "MatchedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OpportunityMatches_SupplierProfileId",
                table: "OpportunityMatches",
                column: "SupplierProfileId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OpportunityMatches");
        }
    }
}
