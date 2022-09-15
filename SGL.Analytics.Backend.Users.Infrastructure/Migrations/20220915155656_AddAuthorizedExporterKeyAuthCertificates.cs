using Microsoft.EntityFrameworkCore.Migrations;
using System;

#nullable disable

namespace SGL.Analytics.Backend.Users.Infrastructure.Migrations {
	public partial class AddAuthorizedExporterKeyAuthCertificates : Migration {
		protected override void Up(MigrationBuilder migrationBuilder) {
			migrationBuilder.CreateTable(
				name: "ExporterKeyAuthCertificates",
				columns: table => new {
					AppId = table.Column<Guid>(type: "uuid", nullable: false),
					PublicKeyId = table.Column<byte[]>(type: "bytea", maxLength: 34, nullable: false),
					Label = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
					CertificatePem = table.Column<string>(type: "text", nullable: false)
				},
				constraints: table => {
					table.PrimaryKey("PK_ExporterKeyAuthCertificates", x => new { x.AppId, x.PublicKeyId });
					table.ForeignKey(
						name: "FK_ExporterKeyAuthCertificates_Applications_AppId",
						column: x => x.AppId,
						principalTable: "Applications",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade);
				});
		}

		protected override void Down(MigrationBuilder migrationBuilder) {
			migrationBuilder.DropTable(
				name: "ExporterKeyAuthCertificates");
		}
	}
}
