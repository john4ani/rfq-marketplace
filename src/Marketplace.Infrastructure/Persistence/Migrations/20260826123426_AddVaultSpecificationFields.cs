using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Marketplace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVaultSpecificationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BillOfQuantitiesUrl",
                table: "VaultDocuments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExtractedMarkdown",
                table: "VaultDocuments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProposalTitle",
                table: "VaultDocuments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TechnicalProposalUrl",
                table: "VaultDocuments",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BillOfQuantitiesUrl",
                table: "VaultDocuments");

            migrationBuilder.DropColumn(
                name: "ExtractedMarkdown",
                table: "VaultDocuments");

            migrationBuilder.DropColumn(
                name: "ProposalTitle",
                table: "VaultDocuments");

            migrationBuilder.DropColumn(
                name: "TechnicalProposalUrl",
                table: "VaultDocuments");
        }
    }
}
