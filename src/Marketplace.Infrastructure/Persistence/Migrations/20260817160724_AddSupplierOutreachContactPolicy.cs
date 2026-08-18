using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Marketplace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierOutreachContactPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OpportunityMatchId",
                table: "SupplierInvitations",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SupplierContactSuppressions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Scope = table.Column<int>(type: "integer", nullable: false),
                    NormalizedEmail = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierContactSuppressions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierContactSuppressions_SupplierCandidates_CandidateId",
                        column: x => x.CandidateId,
                        principalTable: "SupplierCandidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SupplierOutreachContacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    OpportunityMatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvitationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Channel = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    OptOutTokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AttemptedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierOutreachContacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierOutreachContacts_OpportunityMatches_OpportunityMatc~",
                        column: x => x.OpportunityMatchId,
                        principalTable: "OpportunityMatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierOutreachContacts_SupplierCandidates_CandidateId",
                        column: x => x.CandidateId,
                        principalTable: "SupplierCandidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SupplierOutreachContacts_SupplierInvitations_InvitationId",
                        column: x => x.InvitationId,
                        principalTable: "SupplierInvitations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierInvitations_OpportunityMatchId",
                table: "SupplierInvitations",
                column: "OpportunityMatchId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierContactSuppressions_CandidateId_Scope",
                table: "SupplierContactSuppressions",
                columns: new[] { "CandidateId", "Scope" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierContactSuppressions_NormalizedEmail_Scope",
                table: "SupplierContactSuppressions",
                columns: new[] { "NormalizedEmail", "Scope" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierOutreachContacts_CandidateId_CreatedAt",
                table: "SupplierOutreachContacts",
                columns: new[] { "CandidateId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierOutreachContacts_InvitationId",
                table: "SupplierOutreachContacts",
                column: "InvitationId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierOutreachContacts_OpportunityMatchId_Channel",
                table: "SupplierOutreachContacts",
                columns: new[] { "OpportunityMatchId", "Channel" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierOutreachContacts_OptOutTokenHash",
                table: "SupplierOutreachContacts",
                column: "OptOutTokenHash",
                unique: true,
                filter: "\"OptOutTokenHash\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_SupplierInvitations_OpportunityMatches_OpportunityMatchId",
                table: "SupplierInvitations",
                column: "OpportunityMatchId",
                principalTable: "OpportunityMatches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SupplierInvitations_OpportunityMatches_OpportunityMatchId",
                table: "SupplierInvitations");

            migrationBuilder.DropTable(
                name: "SupplierContactSuppressions");

            migrationBuilder.DropTable(
                name: "SupplierOutreachContacts");

            migrationBuilder.DropIndex(
                name: "IX_SupplierInvitations_OpportunityMatchId",
                table: "SupplierInvitations");

            migrationBuilder.DropColumn(
                name: "OpportunityMatchId",
                table: "SupplierInvitations");
        }
    }
}
