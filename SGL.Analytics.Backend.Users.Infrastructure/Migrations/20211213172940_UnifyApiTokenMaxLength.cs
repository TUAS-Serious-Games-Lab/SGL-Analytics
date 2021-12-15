using Microsoft.EntityFrameworkCore.Migrations;

namespace SGL.Analytics.Backend.Users.Infrastructure.Migrations {
	/// <summary>
	/// Extends <c>Applications.ApiToken</c> to 64 chars max length to fix a mismatch of the max length used in log collector service.
	/// </summary>
	public partial class UnifyApiTokenMaxLength : Migration {
		/// <inheritdoc/>
		protected override void Up(MigrationBuilder migrationBuilder) {
			migrationBuilder.AlterColumn<string>(
				name: "ApiToken",
				table: "Applications",
				type: "character varying(64)",
				maxLength: 64,
				nullable: false,
				oldClrType: typeof(string),
				oldType: "character varying(50)",
				oldMaxLength: 50);
		}

		/// <inheritdoc/>
		protected override void Down(MigrationBuilder migrationBuilder) {
			migrationBuilder.AlterColumn<string>(
				name: "ApiToken",
				table: "Applications",
				type: "character varying(50)",
				maxLength: 50,
				nullable: false,
				oldClrType: typeof(string),
				oldType: "character varying(64)",
				oldMaxLength: 64);
		}
	}
}
