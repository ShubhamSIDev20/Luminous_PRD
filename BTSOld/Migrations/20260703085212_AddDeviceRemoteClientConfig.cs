using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BatteryTestingSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceRemoteClientConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClientRemoteIPAddress",
                schema: "Device",
                table: "Devices",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TcpClientRemotePort",
                schema: "Device",
                table: "Devices",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "UdpClientRemotePort",
                schema: "Device",
                table: "Devices",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "UdpStoreRemotePort",
                schema: "Device",
                table: "Devices",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClientRemoteIPAddress",
                schema: "Device",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "TcpClientRemotePort",
                schema: "Device",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "UdpClientRemotePort",
                schema: "Device",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "UdpStoreRemotePort",
                schema: "Device",
                table: "Devices");
        }
    }
}
