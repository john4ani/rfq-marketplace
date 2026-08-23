using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Marketplace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSeapBidResultFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BillOfQuantitiesUrl",
                table: "SeapTenders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProposalTitle",
                table: "SeapTenders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TechnicalProposalUrl",
                table: "SeapTenders",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BillOfQuantitiesUrl",
                table: "SeapTenders");

            migrationBuilder.DropColumn(
                name: "ProposalTitle",
                table: "SeapTenders");

            migrationBuilder.DropColumn(
                name: "TechnicalProposalUrl",
                table: "SeapTenders");
        }
    }
}
