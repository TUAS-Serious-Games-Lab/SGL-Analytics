using Microsoft.EntityFrameworkCore.Migrations;
using System;

#nullable disable

namespace SGL.Analytics.Backend.Logs.Infrastructure.Migrations {
	/// <summary>
	/// Adds the columns and table to store the keys and IVs for end-to-end encryption.
	/// </summary>
	public partial class AddLogEncryptionMetadata : Migration {
		/// <inheritdoc/>
		protected override void Up(MigrationBuilder migrationBuilder) {
			migrationBuilder.AddColumn<byte[]>(
				name: "InitializationVector",
				table: "LogMetadata",
				type: "bytea",
				nullable: true);

			migrationBuilder.AddColumn<byte[]>(
				name: "SharedLogPublicKey",
				table: "LogMetadata",
				type: "bytea",
				nullable: true);

			migrationBuilder.CreateTable(
				name: "LogRecipientKey",
				columns: table => new {
					LogId = table.Column<Guid>(type: "uuid", nullable: false),
					RecipientKeyId = table.Column<byte[]>(type: "bytea", maxLength: 34, nullable: false),
					EncryptionMode = table.Column<int>(type: "integer", nullable: false),
					EncryptedKey = table.Column<byte[]>(type: "bytea", nullable: false),
					LogPublicKey = table.Column<byte[]>(type: "bytea", nullable: true)
				},
				constraints: table => {
					table.PrimaryKey("PK_LogRecipientKey", x => new { x.LogId, x.RecipientKeyId });
					table.ForeignKey(
						name: "FK_LogRecipientKey_LogMetadata_LogId",
						column: x => x.LogId,
						principalTable: "LogMetadata",
						principalColumn: "Id",
						onDelete: ReferentialAction.Cascade);
				});
		}

		/// <inheritdoc/>
		protected override void Down(MigrationBuilder migrationBuilder) {
			migrationBuilder.DropTable(
				name: "LogRecipientKey");

			migrationBuilder.DropColumn(
				name: "InitializationVector",
				table: "LogMetadata");

			migrationBuilder.DropColumn(
				name: "SharedLogPublicKey",
				table: "LogMetadata");
		}
	}
}
