using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BatteryTestingSystem.Migrations
{
    /// <inheritdoc />
    public partial class BatterySessionProperties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BatteryName",
                schema: "Sessions",
                table: "BatterySessions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DbcName",
                schema: "Sessions",
                table: "BatterySessions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProgramName",
                schema: "Sessions",
                table: "BatterySessions",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BatteryName",
                schema: "Sessions",
                table: "BatterySessions");

            migrationBuilder.DropColumn(
                name: "DbcName",
                schema: "Sessions",
                table: "BatterySessions");

            migrationBuilder.DropColumn(
                name: "ProgramName",
                schema: "Sessions",
                table: "BatterySessions");
        }
    }
}
