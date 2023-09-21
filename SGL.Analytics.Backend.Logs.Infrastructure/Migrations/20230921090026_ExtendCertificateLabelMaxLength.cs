using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGL.Analytics.Backend.Logs.Infrastructure.Migrations {
	/// <summary>
	/// Extends the max length of the label columns for recipient certificates.
	/// </summary>
	public partial class ExtendCertificateLabelMaxLength : Migration {
		/// <inheritdoc/>
		protected override void Up(MigrationBuilder migrationBuilder) {
			migrationBuilder.AlterColumn<string>(
				name: "Label",
				table: "Recipient",
				type: "character varying(512)",
				maxLength: 512,
				nullable: false,
				oldClrType: typeof(string),
				oldType: "character varying(128)",
				oldMaxLength: 128);
		}

		/// <inheritdoc/>
		protected override void Down(MigrationBuilder migrationBuilder) {
			migrationBuilder.AlterColumn<string>(
				name: "Label",
				table: "Recipient",
				type: "character varying(128)",
				maxLength: 128,
				nullable: false,
				oldClrType: typeof(string),
				oldType: "character varying(512)",
				oldMaxLength: 512);
		}
	}
}
