﻿using Microsoft.EntityFrameworkCore.Migrations;
using System;

#nullable disable

namespace SGL.Analytics.Backend.Users.Infrastructure.Migrations {
	/// <summary>
	/// Adds the columns needed to support delegated user authentication using a trusted upstream backend.
	/// </summary>
	public partial class AddBasicFederationAuthentication : Migration {
		/// <inheritdoc/>
		protected override void Up(MigrationBuilder migrationBuilder) {
			migrationBuilder.AlterColumn<string>(
				name: "HashedSecret",
				table: "UserRegistrations",
				type: "character varying(128)",
				maxLength: 128,
				nullable: true,
				oldClrType: typeof(string),
				oldType: "character varying(128)",
				oldMaxLength: 128);

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

		/// <inheritdoc/>
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

			migrationBuilder.AlterColumn<string>(
				name: "HashedSecret",
				table: "UserRegistrations",
				type: "character varying(128)",
				maxLength: 128,
				nullable: false,
				defaultValue: "",
				oldClrType: typeof(string),
				oldType: "character varying(128)",
				oldMaxLength: 128,
				oldNullable: true);
		}
	}
}
