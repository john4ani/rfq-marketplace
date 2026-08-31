using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Marketplace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSeapListIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_SeapTenders_PublicationDate",
                table: "SeapTenders",
                column: "PublicationDate",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_SeapTenders_Status_Deadline",
                table: "SeapTenders",
                columns: new[] { "Status", "Deadline" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SeapTenders_PublicationDate",
                table: "SeapTenders");

            migrationBuilder.DropIndex(
                name: "IX_SeapTenders_Status_Deadline",
                table: "SeapTenders");
        }
    }
}
