using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Marketplace.Infrastructure.Persistence;

#nullable disable

namespace Marketplace.Infrastructure.Persistence.Migrations;

[DbContext(typeof(MarketplaceDbContext))]
[Migration("20260724124000_AddRfqItemCategories")]
public partial class AddRfqItemCategories : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "AllowSplit",
            table: "Rfqs",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<Guid>(
            name: "CategoryId",
            table: "RfqItems",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_RfqItems_CategoryId",
            table: "RfqItems",
            column: "CategoryId");

        migrationBuilder.AddForeignKey(
            name: "FK_RfqItems_Categories_CategoryId",
            table: "RfqItems",
            column: "CategoryId",
            principalTable: "Categories",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(name: "FK_RfqItems_Categories_CategoryId", table: "RfqItems");
        migrationBuilder.DropIndex(name: "IX_RfqItems_CategoryId", table: "RfqItems");
        migrationBuilder.DropColumn(name: "CategoryId", table: "RfqItems");
        migrationBuilder.DropColumn(name: "AllowSplit", table: "Rfqs");
    }
}
