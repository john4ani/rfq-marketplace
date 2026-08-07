using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Marketplace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTrustFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccountAuditEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Details = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountAuditEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountAuditEvents_AspNetUsers_AccountId",
                        column: x => x.AccountId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AccountAuditEvents_AspNetUsers_ActorId",
                        column: x => x.ActorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AccountTrustRecords",
                columns: table => new
                {
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    PhoneVerifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PhoneVerifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyVerifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompanyVerifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    VerificationNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountTrustRecords", x => x.AccountId);
                    table.ForeignKey(
                        name: "FK_AccountTrustRecords_AspNetUsers_AccountId",
                        column: x => x.AccountId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AccountTrustRecords_AspNetUsers_CompanyVerifiedBy",
                        column: x => x.CompanyVerifiedBy,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountTrustRecords_AspNetUsers_PhoneVerifiedBy",
                        column: x => x.PhoneVerifiedBy,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountAuditEvents_AccountId_OccurredAt",
                table: "AccountAuditEvents",
                columns: new[] { "AccountId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountAuditEvents_ActorId",
                table: "AccountAuditEvents",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountTrustRecords_CompanyVerifiedBy",
                table: "AccountTrustRecords",
                column: "CompanyVerifiedBy");

            migrationBuilder.CreateIndex(
                name: "IX_AccountTrustRecords_PhoneVerifiedBy",
                table: "AccountTrustRecords",
                column: "PhoneVerifiedBy");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountAuditEvents");

            migrationBuilder.DropTable(
                name: "AccountTrustRecords");
        }
    }
}
