using Microsoft.EntityFrameworkCore.Migrations;

namespace SGL.Analytics.Backend.Logs.Infrastructure.Migrations {
	/// <summary>
	/// Creates the column for the size of the log file content in the LogMetadata table.
	/// </summary>
	public partial class AddLogContentSize : Migration {
		/// <inheritdoc/>
		protected override void Up(MigrationBuilder migrationBuilder) {
			migrationBuilder.AddColumn<long>(
				name: "Size",
				table: "LogMetadata",
				type: "bigint",
				nullable: true);
		}

		/// <inheritdoc/>
		protected override void Down(MigrationBuilder migrationBuilder) {
			migrationBuilder.DropColumn(
				name: "Size",
				table: "LogMetadata");
		}
	}
}
