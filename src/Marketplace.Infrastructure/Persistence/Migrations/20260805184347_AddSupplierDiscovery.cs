using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Marketplace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierDiscovery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SupplierCandidates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyName = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    NormalizedCompanyName = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    ContactEmail = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    PhoneNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Website = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    City = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    County = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReviewedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReviewNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ClaimedBySupplierId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClaimedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierCandidates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierCandidates_AspNetUsers_ClaimedBySupplierId",
                        column: x => x.ClaimedBySupplierId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierCandidates_AspNetUsers_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierCandidates_AspNetUsers_ReviewedBy",
                        column: x => x.ReviewedBy,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierCandidates_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupplierInvitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    RfqId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ClaimedBySupplierId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClaimedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierInvitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierInvitations_AspNetUsers_ClaimedBySupplierId",
                        column: x => x.ClaimedBySupplierId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierInvitations_AspNetUsers_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierInvitations_Rfqs_RfqId",
                        column: x => x.RfqId,
                        principalTable: "Rfqs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierInvitations_SupplierCandidates_CandidateId",
                        column: x => x.CandidateId,
                        principalTable: "SupplierCandidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SupplierDiscoveryAuditEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    InvitationId = table.Column<Guid>(type: "uuid", nullable: true),
                    RfqId = table.Column<Guid>(type: "uuid", nullable: true),
                    Details = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierDiscoveryAuditEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierDiscoveryAuditEvents_AspNetUsers_ActorId",
                        column: x => x.ActorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierDiscoveryAuditEvents_Rfqs_RfqId",
                        column: x => x.RfqId,
                        principalTable: "Rfqs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SupplierDiscoveryAuditEvents_SupplierCandidates_CandidateId",
                        column: x => x.CandidateId,
                        principalTable: "SupplierCandidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SupplierDiscoveryAuditEvents_SupplierInvitations_Invitation~",
                        column: x => x.InvitationId,
                        principalTable: "SupplierInvitations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierCandidates_CategoryId",
                table: "SupplierCandidates",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierCandidates_ClaimedBySupplierId",
                table: "SupplierCandidates",
                column: "ClaimedBySupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierCandidates_CreatedBy",
                table: "SupplierCandidates",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierCandidates_NormalizedCompanyName_County",
                table: "SupplierCandidates",
                columns: new[] { "NormalizedCompanyName", "County" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierCandidates_ReviewedBy",
                table: "SupplierCandidates",
                column: "ReviewedBy");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierCandidates_Status_County_CategoryId",
                table: "SupplierCandidates",
                columns: new[] { "Status", "County", "CategoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierDiscoveryAuditEvents_ActorId",
                table: "SupplierDiscoveryAuditEvents",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierDiscoveryAuditEvents_CandidateId_OccurredAt",
                table: "SupplierDiscoveryAuditEvents",
                columns: new[] { "CandidateId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierDiscoveryAuditEvents_InvitationId",
                table: "SupplierDiscoveryAuditEvents",
                column: "InvitationId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierDiscoveryAuditEvents_RfqId",
                table: "SupplierDiscoveryAuditEvents",
                column: "RfqId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierInvitations_CandidateId_RfqId",
                table: "SupplierInvitations",
                columns: new[] { "CandidateId", "RfqId" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierInvitations_ClaimedBySupplierId",
                table: "SupplierInvitations",
                column: "ClaimedBySupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierInvitations_CreatedBy",
                table: "SupplierInvitations",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierInvitations_RfqId",
                table: "SupplierInvitations",
                column: "RfqId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierInvitations_TokenHash",
                table: "SupplierInvitations",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SupplierDiscoveryAuditEvents");

            migrationBuilder.DropTable(
                name: "SupplierInvitations");

            migrationBuilder.DropTable(
                name: "SupplierCandidates");
        }
    }
}
