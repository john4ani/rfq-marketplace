using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Marketplace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAssistedIntakeSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiIntakeSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BuyerId = table.Column<Guid>(type: "uuid", nullable: false),
                    InitialInput = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    TranscriptJson = table.Column<string>(type: "jsonb", nullable: false),
                    ExtractionJson = table.Column<string>(type: "jsonb", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Provider = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Model = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    PromptVersion = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    InputTokens = table.Column<int>(type: "integer", nullable: false),
                    OutputTokens = table.Column<int>(type: "integer", nullable: false),
                    ApprovedRfqId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiIntakeSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiIntakeSessions_BuyerProfiles_BuyerId",
                        column: x => x.BuyerId,
                        principalTable: "BuyerProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AiIntakeSessions_Rfqs_ApprovedRfqId",
                        column: x => x.ApprovedRfqId,
                        principalTable: "Rfqs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiIntakeSessions_ApprovedRfqId",
                table: "AiIntakeSessions",
                column: "ApprovedRfqId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiIntakeSessions_BuyerId_UpdatedAt",
                table: "AiIntakeSessions",
                columns: new[] { "BuyerId", "UpdatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiIntakeSessions");
        }
    }
}
