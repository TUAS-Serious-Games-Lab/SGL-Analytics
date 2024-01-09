using Microsoft.EntityFrameworkCore.Migrations;
using System;

#nullable disable

namespace SGL.Analytics.Backend.Users.Infrastructure.Migrations {
	/// <summary>
	/// Adds the SignerCertificates table that stores app-specific signer certificates for associated app registrations,
	/// which are used to validate exporter certificates during key authentication.
	/// </summary>
	public partial class AddPerAppSignerCertificatesForKeyAuth : Migration {
		/// <inheritdoc/>
		protected override void Up(MigrationBuilder migrationBuilder) {
			migrationBuilder.CreateTable(
				name: "SignerCertificates",
				columns: table => new {
					AppId = table.Column<Guid>(type: "uuid", nullable: false),
					PublicKeyId = table.Column<byte[]>(type: "bytea", maxLength: 34, nullable: false),
					CertificatePem = table.Column<string>(type: "text", nullable: false),
					Label = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false)
				},
				constraints: table => {
					table.PrimaryKey("PK_SignerCertificates", x => new { x.AppId, x.PublicKeyId });
					table.ForeignKey(
						name: "FK_SignerCertificates_Applications_AppId",
						column: x => x.AppId,
						principalTable: "Applications",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade);
				});
		}
		/// <inheritdoc/>
		protected override void Down(MigrationBuilder migrationBuilder) {
			migrationBuilder.DropTable(
				name: "SignerCertificates");
		}
	}
}
