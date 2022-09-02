using Microsoft.EntityFrameworkCore.Migrations;
using System;

#nullable disable

namespace SGL.Analytics.Backend.Logs.Infrastructure.Migrations {
	public partial class EFCoreModellingUpdate : Migration {
		protected override void Up(MigrationBuilder migrationBuilder) {
			migrationBuilder.AlterColumn<DateTime>(
				name: "UploadTime",
				table: "LogMetadata",
				type: "timestamp with time zone",
				nullable: false,
				oldClrType: typeof(DateTime),
				oldType: "timestamp without time zone");

			migrationBuilder.AlterColumn<DateTime>(
				name: "EndTime",
				table: "LogMetadata",
				type: "timestamp with time zone",
				nullable: false,
				oldClrType: typeof(DateTime),
				oldType: "timestamp without time zone");

			migrationBuilder.AlterColumn<DateTime>(
				name: "CreationTime",
				table: "LogMetadata",
				type: "timestamp with time zone",
				nullable: false,
				oldClrType: typeof(DateTime),
				oldType: "timestamp without time zone");
		}

		protected override void Down(MigrationBuilder migrationBuilder) {
			migrationBuilder.AlterColumn<DateTime>(
				name: "UploadTime",
				table: "LogMetadata",
				type: "timestamp without time zone",
				nullable: false,
				oldClrType: typeof(DateTime),
				oldType: "timestamp with time zone");

			migrationBuilder.AlterColumn<DateTime>(
				name: "EndTime",
				table: "LogMetadata",
				type: "timestamp without time zone",
				nullable: false,
				oldClrType: typeof(DateTime),
				oldType: "timestamp with time zone");

			migrationBuilder.AlterColumn<DateTime>(
				name: "CreationTime",
				table: "LogMetadata",
				type: "timestamp without time zone",
				nullable: false,
				oldClrType: typeof(DateTime),
				oldType: "timestamp with time zone");
		}
	}
}
