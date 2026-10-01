using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BatteryTestingSystem.Migrations
{
    /// <inheritdoc />
    public partial class DbcFiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "FileHash",
                schema: "Files",
                table: "DBCFiles",
                newName: "Version");

            migrationBuilder.AddColumn<long>(
                name: "BatteryId",
                schema: "Files",
                table: "DBCFiles",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "FileSizeBytes",
                schema: "Files",
                table: "DBCFiles",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "OriginalFileName",
                schema: "Files",
                table: "DBCFiles",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_DBCFiles_BatteryId",
                schema: "Files",
                table: "DBCFiles",
                column: "BatteryId");

            migrationBuilder.AddForeignKey(
                name: "FK_DBCFiles_Batteries_BatteryId",
                schema: "Files",
                table: "DBCFiles",
                column: "BatteryId",
                principalSchema: "Battery",
                principalTable: "Batteries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DBCFiles_Batteries_BatteryId",
                schema: "Files",
                table: "DBCFiles");

            migrationBuilder.DropIndex(
                name: "IX_DBCFiles_BatteryId",
                schema: "Files",
                table: "DBCFiles");

            migrationBuilder.DropColumn(
                name: "BatteryId",
                schema: "Files",
                table: "DBCFiles");

            migrationBuilder.DropColumn(
                name: "FileSizeBytes",
                schema: "Files",
                table: "DBCFiles");

            migrationBuilder.DropColumn(
                name: "OriginalFileName",
                schema: "Files",
                table: "DBCFiles");

            migrationBuilder.RenameColumn(
                name: "Version",
                schema: "Files",
                table: "DBCFiles",
                newName: "FileHash");
        }
    }
}
