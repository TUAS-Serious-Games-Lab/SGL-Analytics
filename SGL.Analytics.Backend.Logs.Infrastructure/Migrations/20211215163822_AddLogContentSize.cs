using Microsoft.EntityFrameworkCore.Migrations;

namespace SGL.Analytics.Backend.Logs.Infrastructure.Migrations
{
    public partial class AddLogContentSize : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "Size",
                table: "LogMetadata",
                type: "bigint",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Size",
                table: "LogMetadata");
        }
    }
}
