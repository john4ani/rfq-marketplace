using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Marketplace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddConciergeOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConciergeMatchOverrides",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RfqId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    AdministratorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConciergeMatchOverrides", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConciergeMatchOverrides_AspNetUsers_AdministratorId",
                        column: x => x.AdministratorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConciergeMatchOverrides_Rfqs_RfqId",
                        column: x => x.RfqId,
                        principalTable: "Rfqs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ConciergeMatchOverrides_SupplierProfiles_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "SupplierProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SubjectType = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: true),
                    Metadata = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductEvents_AspNetUsers_ActorId",
                        column: x => x.ActorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConciergeMatchOverrides_AdministratorId",
                table: "ConciergeMatchOverrides",
                column: "AdministratorId");

            migrationBuilder.CreateIndex(
                name: "IX_ConciergeMatchOverrides_RfqId_SupplierId",
                table: "ConciergeMatchOverrides",
                columns: new[] { "RfqId", "SupplierId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConciergeMatchOverrides_SupplierId",
                table: "ConciergeMatchOverrides",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductEvents_ActorId",
                table: "ProductEvents",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductEvents_Name_OccurredAt",
                table: "ProductEvents",
                columns: new[] { "Name", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductEvents_SubjectType_SubjectId_OccurredAt",
                table: "ProductEvents",
                columns: new[] { "SubjectType", "SubjectId", "OccurredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConciergeMatchOverrides");

            migrationBuilder.DropTable(
                name: "ProductEvents");
        }
    }
}
