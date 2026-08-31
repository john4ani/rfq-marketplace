using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Marketplace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSeapAiDetailExtraction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AwardCriterion",
                table: "SeapTenders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContractDuration",
                table: "SeapTenders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FundingSource",
                table: "SeapTenders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KeyPersonnel",
                table: "SeapTenders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ParticipationGuarantee",
                table: "SeapTenders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QualificationRequirements",
                table: "SeapTenders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string[]>(
                name: "SummaryPoints",
                table: "SeapTenders",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AwardCriterion",
                table: "SeapTenders");

            migrationBuilder.DropColumn(
                name: "ContractDuration",
                table: "SeapTenders");

            migrationBuilder.DropColumn(
                name: "FundingSource",
                table: "SeapTenders");

            migrationBuilder.DropColumn(
                name: "KeyPersonnel",
                table: "SeapTenders");

            migrationBuilder.DropColumn(
                name: "ParticipationGuarantee",
                table: "SeapTenders");

            migrationBuilder.DropColumn(
                name: "QualificationRequirements",
                table: "SeapTenders");

            migrationBuilder.DropColumn(
                name: "SummaryPoints",
                table: "SeapTenders");
        }
    }
}
