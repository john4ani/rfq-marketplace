using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Marketplace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MakeProcurementUserOwned : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AiIntakeSessions_BuyerProfiles_BuyerId",
                table: "AiIntakeSessions");

            migrationBuilder.DropForeignKey(
                name: "FK_Rfqs_BuyerProfiles_BuyerId",
                table: "Rfqs");

            migrationBuilder.AddForeignKey(
                name: "FK_AiIntakeSessions_AspNetUsers_BuyerId",
                table: "AiIntakeSessions",
                column: "BuyerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Rfqs_AspNetUsers_BuyerId",
                table: "Rfqs",
                column: "BuyerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AiIntakeSessions_AspNetUsers_BuyerId",
                table: "AiIntakeSessions");

            migrationBuilder.DropForeignKey(
                name: "FK_Rfqs_AspNetUsers_BuyerId",
                table: "Rfqs");

            migrationBuilder.AddForeignKey(
                name: "FK_AiIntakeSessions_BuyerProfiles_BuyerId",
                table: "AiIntakeSessions",
                column: "BuyerId",
                principalTable: "BuyerProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Rfqs_BuyerProfiles_BuyerId",
                table: "Rfqs",
                column: "BuyerId",
                principalTable: "BuyerProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
