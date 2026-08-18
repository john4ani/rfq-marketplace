using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Marketplace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MakeInvitationOpportunityLinkOptional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SupplierInvitations_OpportunityMatches_OpportunityMatchId",
                table: "SupplierInvitations");

            migrationBuilder.AddForeignKey(
                name: "FK_SupplierInvitations_OpportunityMatches_OpportunityMatchId",
                table: "SupplierInvitations",
                column: "OpportunityMatchId",
                principalTable: "OpportunityMatches",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SupplierInvitations_OpportunityMatches_OpportunityMatchId",
                table: "SupplierInvitations");

            migrationBuilder.AddForeignKey(
                name: "FK_SupplierInvitations_OpportunityMatches_OpportunityMatchId",
                table: "SupplierInvitations",
                column: "OpportunityMatchId",
                principalTable: "OpportunityMatches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
