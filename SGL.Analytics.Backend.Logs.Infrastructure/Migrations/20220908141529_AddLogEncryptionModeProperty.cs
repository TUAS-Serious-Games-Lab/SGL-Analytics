using Microsoft.EntityFrameworkCore.Migrations;
using System;

#nullable disable

namespace SGL.Analytics.Backend.Logs.Infrastructure.Migrations {
	public partial class AddLogEncryptionModeProperty : Migration {
		protected override void Up(MigrationBuilder migrationBuilder) {
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
