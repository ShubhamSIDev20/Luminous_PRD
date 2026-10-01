using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BatteryTestingSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionPortDbc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "Port2DbcFileRecordID",
                schema: "Sessions",
                table: "BatterySessions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Port2DbcName",
                schema: "Sessions",
                table: "BatterySessions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "Port3DbcFileRecordID",
                schema: "Sessions",
                table: "BatterySessions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Port3DbcName",
                schema: "Sessions",
                table: "BatterySessions",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Port2DbcFileRecordID",
                schema: "Sessions",
                table: "BatterySessions");

            migrationBuilder.DropColumn(
                name: "Port2DbcName",
                schema: "Sessions",
                table: "BatterySessions");

            migrationBuilder.DropColumn(
                name: "Port3DbcFileRecordID",
                schema: "Sessions",
                table: "BatterySessions");

            migrationBuilder.DropColumn(
                name: "Port3DbcName",
                schema: "Sessions",
                table: "BatterySessions");
        }
    }
}
