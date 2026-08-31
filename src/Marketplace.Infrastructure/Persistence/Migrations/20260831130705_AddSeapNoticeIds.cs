using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Marketplace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSeapNoticeIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "NoticeId",
                table: "SeapTenders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SysNoticeTypeId",
                table: "SeapTenders",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NoticeId",
                table: "SeapTenders");

            migrationBuilder.DropColumn(
                name: "SysNoticeTypeId",
                table: "SeapTenders");
        }
    }
}
