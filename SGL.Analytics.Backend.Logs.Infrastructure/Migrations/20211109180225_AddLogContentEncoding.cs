using Microsoft.EntityFrameworkCore.Migrations;

namespace SGL.Analytics.Backend.Logs.Infrastructure.Migrations {
	/// <summary>
	/// Creates the column for storing the encoding of the log file content in the LogMetadata table.
	/// </summary>
	public partial class AddLogContentEncoding : Migration {
		/// <inheritdoc/>
		protected override void Up(MigrationBuilder migrationBuilder) {
			migrationBuilder.AddColumn<int>(
				name: "Encoding",
				table: "LogMetadata",
				type: "integer",
				nullable: false,
				defaultValue: 1);
		}

		/// <inheritdoc/>
		protected override void Down(MigrationBuilder migrationBuilder) {
			migrationBuilder.DropColumn(
				name: "Encoding",
				table: "LogMetadata");
		}
	}
}
