using Microsoft.EntityFrameworkCore.Migrations;
using System;

#nullable disable

namespace SGL.Analytics.Backend.Logs.Infrastructure.Migrations {
	/// <summary>
	/// Updates the mapping handling of timestamp values and timezones for EF Core 6 Npgsql.
	/// </summary>
	public partial class EFCoreModellingUpdate : Migration {
		/// <inheritdoc/>
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

		/// <inheritdoc/>
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
