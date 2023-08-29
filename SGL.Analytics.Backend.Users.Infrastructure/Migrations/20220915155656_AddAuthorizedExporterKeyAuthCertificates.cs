using Microsoft.EntityFrameworkCore.Migrations;
using System;

#nullable disable

namespace SGL.Analytics.Backend.Users.Infrastructure.Migrations {
	/// <summary>
	/// Adds the table that stores the certificates for key-pair based exporter authentication for the registered applications.
	/// </summary>
	public partial class AddAuthorizedExporterKeyAuthCertificates : Migration {
		/// <inheritdoc/>
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

		/// <inheritdoc/>
		protected override void Down(MigrationBuilder migrationBuilder) {
			migrationBuilder.DropTable(
				name: "ExporterKeyAuthCertificates");
		}
	}
}
