using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace SGL.Analytics.Backend.Logs.Infrastructure.Migrations
{
    public partial class AddRecipientKeyTable : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Recipient",
                columns: table => new
                {
                    AppId = table.Column<Guid>(type: "uuid", nullable: false),
                    PublicKeyId = table.Column<byte[]>(type: "bytea", maxLength: 33, nullable: false),
                    Label = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CertificatePem = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Recipient", x => new { x.AppId, x.PublicKeyId });
                    table.ForeignKey(
                        name: "FK_Recipient_Applications_AppId",
                        column: x => x.AppId,
                        principalTable: "Applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Recipient");
        }
    }
}
