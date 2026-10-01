using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BatteryTestingSystem.Migrations
{
    /// <inheritdoc />
    public partial class AlarmLogTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AlarmLog",
                schema: "Audit",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AlarmKey = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Severity = table.Column<int>(type: "INTEGER", nullable: false),
                    Source = table.Column<int>(type: "INTEGER", nullable: false),
                    DeviceId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    BoardNumber = table.Column<int>(type: "INTEGER", nullable: true),
                    ChannelNumber = table.Column<int>(type: "INTEGER", nullable: true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    Message = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    FirstSeenUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastSeenUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    OccurrenceCount = table.Column<int>(type: "INTEGER", nullable: false),
                    AcknowledgedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    AcknowledgedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ClearedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlarmLog", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AlarmLog_AcknowledgedAtUtc_ClearedAtUtc_LastSeenUtc",
                schema: "Audit",
                table: "AlarmLog",
                columns: new[] { "AcknowledgedAtUtc", "ClearedAtUtc", "LastSeenUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AlarmLog_AlarmKey",
                schema: "Audit",
                table: "AlarmLog",
                column: "AlarmKey");

            migrationBuilder.CreateIndex(
                name: "IX_AlarmLog_LastSeenUtc",
                schema: "Audit",
                table: "AlarmLog",
                column: "LastSeenUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AlarmLog",
                schema: "Audit");
        }
    }
}
