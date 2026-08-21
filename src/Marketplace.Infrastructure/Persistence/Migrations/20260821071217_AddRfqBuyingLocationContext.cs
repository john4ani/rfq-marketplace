using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Marketplace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRfqBuyingLocationContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BuyingCompanyId",
                table: "Rfqs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LocationContext",
                table: "Rfqs",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Other");

            migrationBuilder.CreateIndex(
                name: "IX_Rfqs_BuyingCompanyId",
                table: "Rfqs",
                column: "BuyingCompanyId");

            migrationBuilder.AddForeignKey(
                name: "FK_Rfqs_SupplierProfiles_BuyingCompanyId",
                table: "Rfqs",
                column: "BuyingCompanyId",
                principalTable: "SupplierProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Rfqs_SupplierProfiles_BuyingCompanyId",
                table: "Rfqs");

            migrationBuilder.DropIndex(
                name: "IX_Rfqs_BuyingCompanyId",
                table: "Rfqs");

            migrationBuilder.DropColumn(
                name: "BuyingCompanyId",
                table: "Rfqs");

            migrationBuilder.DropColumn(
                name: "LocationContext",
                table: "Rfqs");
        }
    }
}
