using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BatteryTestingSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduleDbcPorts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "DbcFileId",
                table: "ProgramSchedules",
                newName: "Port3DbcFileId");

            migrationBuilder.AddColumn<long>(
                name: "Port1DbcFileId",
                table: "ProgramSchedules",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "Port2DbcFileId",
                table: "ProgramSchedules",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Port1DbcFileId",
                table: "ProgramSchedules");

            migrationBuilder.DropColumn(
                name: "Port2DbcFileId",
                table: "ProgramSchedules");

            migrationBuilder.RenameColumn(
                name: "Port3DbcFileId",
                table: "ProgramSchedules",
                newName: "DbcFileId");
        }
    }
}
