using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Marketplace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDeliveryConfirmation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeliveredAt",
                table: "Rfqs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeliveryReportedAt",
                table: "Rfqs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DeliveryRequired",
                table: "Rfqs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "FulfillmentStatus",
                table: "Rfqs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "DeliveryIncluded",
                table: "Offers",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeliveredAt",
                table: "Rfqs");

            migrationBuilder.DropColumn(
                name: "DeliveryReportedAt",
                table: "Rfqs");

            migrationBuilder.DropColumn(
                name: "DeliveryRequired",
                table: "Rfqs");

            migrationBuilder.DropColumn(
                name: "FulfillmentStatus",
                table: "Rfqs");

            migrationBuilder.DropColumn(
                name: "DeliveryIncluded",
                table: "Offers");
        }
    }
}
