using Microsoft.EntityFrameworkCore.Migrations;
using System;

#nullable disable

namespace SGL.Analytics.Backend.Users.Infrastructure.Migrations {
	public partial class EFCoreModellingUpdate : Migration {
		protected override void Up(MigrationBuilder migrationBuilder) {
			migrationBuilder.AlterColumn<DateTime>(
				name: "DateTimeValue",
				table: "ApplicationUserPropertyInstances",
				type: "timestamp with time zone",
				nullable: true,
				oldClrType: typeof(DateTime),
				oldType: "timestamp without time zone",
				oldNullable: true);
		}

		protected override void Down(MigrationBuilder migrationBuilder) {
			migrationBuilder.AlterColumn<DateTime>(
				name: "DateTimeValue",
				table: "ApplicationUserPropertyInstances",
				type: "timestamp without time zone",
				nullable: true,
				oldClrType: typeof(DateTime),
				oldType: "timestamp with time zone",
				oldNullable: true);
		}
	}
}
