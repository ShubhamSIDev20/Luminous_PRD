using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BatteryTestingSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddBatteryPortAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "Port1DbcId",
                schema: "Battery",
                table: "Batteries",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "Port2DbcId",
                schema: "Battery",
                table: "Batteries",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "Port3DbcId",
                schema: "Battery",
                table: "Batteries",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Port1DbcId",
                schema: "Battery",
                table: "Batteries");

            migrationBuilder.DropColumn(
                name: "Port2DbcId",
                schema: "Battery",
                table: "Batteries");

            migrationBuilder.DropColumn(
                name: "Port3DbcId",
                schema: "Battery",
                table: "Batteries");
        }
    }
}
