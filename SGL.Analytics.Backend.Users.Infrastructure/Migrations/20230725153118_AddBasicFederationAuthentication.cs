using Microsoft.EntityFrameworkCore.Migrations;
using System;

#nullable disable

namespace SGL.Analytics.Backend.Users.Infrastructure.Migrations {
	public partial class AddBasicFederationAuthentication : Migration {
		protected override void Up(MigrationBuilder migrationBuilder) {
			migrationBuilder.AddColumn<Guid>(
				name: "BasicFederationUpstreamUserId",
				table: "UserRegistrations",
				type: "uuid",
				nullable: true);

			migrationBuilder.AddColumn<string>(
				name: "BasicFederationUpstreamAuthUrl",
				table: "Applications",
				type: "character varying(255)",
				maxLength: 255,
				nullable: true);

			migrationBuilder.CreateIndex(
				name: "IX_UserRegistrations_AppId_BasicFederationUpstreamUserId",
				table: "UserRegistrations",
				columns: new[] { "AppId", "BasicFederationUpstreamUserId" },
				unique: true);
		}

		protected override void Down(MigrationBuilder migrationBuilder) {
			migrationBuilder.DropIndex(
				name: "IX_UserRegistrations_AppId_BasicFederationUpstreamUserId",
				table: "UserRegistrations");

			migrationBuilder.DropColumn(
				name: "BasicFederationUpstreamUserId",
				table: "UserRegistrations");

			migrationBuilder.DropColumn(
				name: "BasicFederationUpstreamAuthUrl",
				table: "Applications");
		}
	}
}
