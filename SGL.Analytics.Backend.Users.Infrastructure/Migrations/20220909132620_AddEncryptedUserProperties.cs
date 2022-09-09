using Microsoft.EntityFrameworkCore.Migrations;
using System;

#nullable disable

namespace SGL.Analytics.Backend.Users.Infrastructure.Migrations {
	public partial class AddEncryptedUserProperties : Migration {
		protected override void Up(MigrationBuilder migrationBuilder) {
			migrationBuilder.AddColumn<byte[]>(
				name: "EncryptedProperties",
				table: "UserRegistrations",
				type: "bytea",
				nullable: false,
				defaultValue: new byte[0]);

			migrationBuilder.AddColumn<int>(
				name: "PropertyEncryptionMode",
				table: "UserRegistrations",
				type: "integer",
				nullable: false,
				defaultValue: 0);

			migrationBuilder.AddColumn<byte[]>(
				name: "PropertyInitializationVector",
				table: "UserRegistrations",
				type: "bytea",
				nullable: false,
				defaultValue: new byte[0]);

			migrationBuilder.AddColumn<byte[]>(
				name: "PropertySharedPublicKey",
				table: "UserRegistrations",
				type: "bytea",
				nullable: true);

			migrationBuilder.CreateTable(
				name: "UserPropertyRecipientKeys",
				columns: table => new {
					UserId = table.Column<Guid>(type: "uuid", nullable: false),
					RecipientKeyId = table.Column<byte[]>(type: "bytea", maxLength: 34, nullable: false),
					EncryptionMode = table.Column<int>(type: "integer", nullable: false),
					EncryptedKey = table.Column<byte[]>(type: "bytea", nullable: false),
					UserPropertiesPublicKey = table.Column<byte[]>(type: "bytea", nullable: true)
				},
				constraints: table => {
					table.PrimaryKey("PK_UserPropertyRecipientKeys", x => new { x.UserId, x.RecipientKeyId });
					table.ForeignKey(
						name: "FK_UserPropertyRecipientKeys_UserRegistrations_UserId",
						column: x => x.UserId,
						principalTable: "UserRegistrations",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade);
				});
		}

		protected override void Down(MigrationBuilder migrationBuilder) {
			migrationBuilder.DropTable(
				name: "UserPropertyRecipientKeys");

			migrationBuilder.DropColumn(
				name: "EncryptedProperties",
				table: "UserRegistrations");

			migrationBuilder.DropColumn(
				name: "PropertyEncryptionMode",
				table: "UserRegistrations");

			migrationBuilder.DropColumn(
				name: "PropertyInitializationVector",
				table: "UserRegistrations");

			migrationBuilder.DropColumn(
				name: "PropertySharedPublicKey",
				table: "UserRegistrations");
		}
	}
}
