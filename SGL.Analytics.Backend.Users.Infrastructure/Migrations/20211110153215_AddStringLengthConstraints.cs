using Microsoft.EntityFrameworkCore.Migrations;

namespace SGL.Analytics.Backend.Users.Infrastructure.Migrations {
	/// <summary>
	/// Adds reasonable length constraints to string fields where they were missing.
	/// </summary>
	public partial class AddStringLengthConstraints : Migration {
		/// <inheritdoc/>
		protected override void Up(MigrationBuilder migrationBuilder) {
			migrationBuilder.AlterColumn<string>(
				name: "Username",
				table: "UserRegistrations",
				type: "character varying(64)",
				maxLength: 64,
				nullable: false,
				oldClrType: typeof(string),
				oldType: "text");

			migrationBuilder.AlterColumn<string>(
				name: "HashedSecret",
				table: "UserRegistrations",
				type: "character varying(128)",
				maxLength: 128,
				nullable: false,
				oldClrType: typeof(string),
				oldType: "text");

			migrationBuilder.AlterColumn<string>(
				name: "Name",
				table: "ApplicationUserPropertyDefinitions",
				type: "character varying(128)",
				maxLength: 128,
				nullable: false,
				oldClrType: typeof(string),
				oldType: "text");

			migrationBuilder.AlterColumn<string>(
				name: "Name",
				table: "Applications",
				type: "character varying(128)",
				maxLength: 128,
				nullable: false,
				oldClrType: typeof(string),
				oldType: "text");

			migrationBuilder.AlterColumn<string>(
				name: "ApiToken",
				table: "Applications",
				type: "character varying(50)",
				maxLength: 50,
				nullable: false,
				oldClrType: typeof(string),
				oldType: "text");
		}

		/// <inheritdoc/>
		protected override void Down(MigrationBuilder migrationBuilder) {
			migrationBuilder.AlterColumn<string>(
				name: "Username",
				table: "UserRegistrations",
				type: "text",
				nullable: false,
				oldClrType: typeof(string),
				oldType: "character varying(64)",
				oldMaxLength: 64);

			migrationBuilder.AlterColumn<string>(
				name: "HashedSecret",
				table: "UserRegistrations",
				type: "text",
				nullable: false,
				oldClrType: typeof(string),
				oldType: "character varying(128)",
				oldMaxLength: 128);

			migrationBuilder.AlterColumn<string>(
				name: "Name",
				table: "ApplicationUserPropertyDefinitions",
				type: "text",
				nullable: false,
				oldClrType: typeof(string),
				oldType: "character varying(128)",
				oldMaxLength: 128);

			migrationBuilder.AlterColumn<string>(
				name: "Name",
				table: "Applications",
				type: "text",
				nullable: false,
				oldClrType: typeof(string),
				oldType: "character varying(128)",
				oldMaxLength: 128);

			migrationBuilder.AlterColumn<string>(
				name: "ApiToken",
				table: "Applications",
				type: "text",
				nullable: false,
				oldClrType: typeof(string),
				oldType: "character varying(50)",
				oldMaxLength: 50);
		}
	}
}
