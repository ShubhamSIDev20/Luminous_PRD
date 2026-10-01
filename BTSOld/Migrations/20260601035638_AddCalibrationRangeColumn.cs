using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BatteryTestingSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddCalibrationRangeColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Range",
                schema: "Device",
                table: "CalibrationData",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Range",
                schema: "Device",
                table: "CalibrationData");
        }
    }
}
