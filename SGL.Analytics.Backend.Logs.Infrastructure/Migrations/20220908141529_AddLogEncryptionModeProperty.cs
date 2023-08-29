using Microsoft.EntityFrameworkCore.Migrations;
using System;

#nullable disable

namespace SGL.Analytics.Backend.Logs.Infrastructure.Migrations {
	/// <summary>
	/// Adds the encryption mode column and makes the IV column required, setting an empty byte sequence for unencrypted legacy records.
	/// </summary>
	public partial class AddLogEncryptionModeProperty : Migration {
		/// <inheritdoc/>
		protected override void Up(MigrationBuilder migrationBuilder) {
			migrationBuilder.Sql(@"UPDATE ""LogMetadata"" SET ""InitializationVector"" = E'\\x' WHERE ""InitializationVector"" IS NULL;");

			migrationBuilder.AlterColumn<byte[]>(
				name: "InitializationVector",
				table: "LogMetadata",
				type: "bytea",
				nullable: false,
				defaultValue: new byte[0],
				oldClrType: typeof(byte[]),
				oldType: "bytea",
				oldNullable: true);

			migrationBuilder.AddColumn<int>(
				name: "EncryptionMode",
				table: "LogMetadata",
				type: "integer",
				nullable: false,
				defaultValue: 0);
		}

		/// <inheritdoc/>
		protected override void Down(MigrationBuilder migrationBuilder) {
			migrationBuilder.DropColumn(
				name: "EncryptionMode",
				table: "LogMetadata");

			migrationBuilder.AlterColumn<byte[]>(
				name: "InitializationVector",
				table: "LogMetadata",
				type: "bytea",
				nullable: true,
				oldClrType: typeof(byte[]),
				oldType: "bytea");
		}
	}
}
