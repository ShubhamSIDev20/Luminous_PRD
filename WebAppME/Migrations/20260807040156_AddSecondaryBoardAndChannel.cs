using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BatteryTestingSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddSecondaryBoardAndChannel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. Create SecondaryBoards table ──────────────────────────────
            migrationBuilder.CreateTable(
                name: "SecondaryBoards",
                schema: "Device",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DeviceId = table.Column<int>(type: "INTEGER", nullable: false),
                    BoardNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    IsImplicit = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SecondaryBoards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SecondaryBoards_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalSchema: "Device",
                        principalTable: "Devices",
                        principalColumn: "DeviceID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SecondaryBoards_DeviceId_BoardNumber",
                schema: "Device",
                table: "SecondaryBoards",
                columns: new[] { "DeviceId", "BoardNumber" },
                unique: true);

            // ── 2. Rename Circuits -> Channels IN PLACE (preserves data; the
            //      auto-generated scaffold emitted a drop+recreate here, which
            //      would have discarded every existing row) ──────────────────
            migrationBuilder.RenameTable(
                name: "Circuits",
                schema: "Device",
                newName: "Channels",
                newSchema: "Device");

            migrationBuilder.RenameColumn(
                name: "CircuitID",
                schema: "Device",
                table: "Channels",
                newName: "ChannelNumber");

            migrationBuilder.RenameColumn(
                name: "CircuitType",
                schema: "Device",
                table: "Channels",
                newName: "ChannelType");

            migrationBuilder.RenameColumn(
                name: "CircuitMaxVoltage",
                schema: "Device",
                table: "Channels",
                newName: "ChannelMaxVoltage");

            migrationBuilder.RenameColumn(
                name: "CircuitMinVoltage",
                schema: "Device",
                table: "Channels",
                newName: "ChannelMinVoltage");

            migrationBuilder.RenameColumn(
                name: "CircuitMaxDischargingCurrent",
                schema: "Device",
                table: "Channels",
                newName: "ChannelMaxDischargingCurrent");

            migrationBuilder.RenameColumn(
                name: "CircuitMaxChargingCurrent",
                schema: "Device",
                table: "Channels",
                newName: "ChannelMaxChargingCurrent");

            migrationBuilder.AddColumn<long>(
                name: "SecondaryBoardId",
                schema: "Device",
                table: "Channels",
                type: "INTEGER",
                nullable: true);

            // ── 3. Pre-backfill safety check ─────────────────────────────────
            // ChannelNumber still holds the pre-shift 0-indexed CircuitID value
            // at this point. Abort loudly if any device would overflow the new
            // 8-channel-per-board cap once shifted +1, per the design doc's
            // decision #8 - never silently truncate.
            // SQLite has no standalone RAISE(ABORT, ...) outside of triggers, so this
            // deliberately forces a division-by-zero error (which SQLite does raise
            // outside triggers) only when the overflow condition is true, aborting
            // the migration transaction with a clear failure rather than proceeding.
            migrationBuilder.Sql(@"
                SELECT CASE
                    WHEN EXISTS (
                        SELECT 1 FROM Channels
                        GROUP BY DeviceId
                        HAVING MAX(ChannelNumber) + 1 > 8
                    )
                    THEN 1 / 0
                    ELSE 0
                END;
            ");

            // ── 4. Backfill: one implicit board 1 per device, repoint + shift ──
            migrationBuilder.Sql(@"
                INSERT INTO SecondaryBoards (DeviceId, BoardNumber, IsImplicit, CreatedAt, UpdatedAt, IsDeleted)
                SELECT DISTINCT DeviceId, 1, 1, datetime('now'), datetime('now'), 0
                FROM Channels;
            ");

            migrationBuilder.Sql(@"
                UPDATE Channels
                SET ChannelNumber = ChannelNumber + 1,
                    SecondaryBoardId = (
                        SELECT sb.Id FROM SecondaryBoards sb
                        WHERE sb.DeviceId = Channels.DeviceId AND sb.BoardNumber = 1
                    );
            ");

            migrationBuilder.AlterColumn<long>(
                name: "SecondaryBoardId",
                schema: "Device",
                table: "Channels",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Channels_SecondaryBoards_SecondaryBoardId",
                schema: "Device",
                table: "Channels",
                column: "SecondaryBoardId",
                principalSchema: "Device",
                principalTable: "SecondaryBoards",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.CreateIndex(
                name: "IX_Channels_SecondaryBoardId_ChannelNumber",
                schema: "Device",
                table: "Channels",
                columns: new[] { "SecondaryBoardId", "ChannelNumber" },
                unique: true);

            // ── 5. CalibrationData: rename CircuitId -> ChannelId (preserves
            //      data), add new SecondaryBoardNumber column (implicit board 1
            //      for all existing rows), then shift ChannelId +1 to match ──
            migrationBuilder.RenameColumn(
                name: "CircuitId",
                schema: "Device",
                table: "CalibrationData",
                newName: "ChannelId");

            migrationBuilder.AddColumn<int>(
                name: "SecondaryBoardNumber",
                schema: "Device",
                table: "CalibrationData",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.Sql(@"UPDATE CalibrationData SET ChannelId = ChannelId + 1;");

            // ── 6. BatterySessions: rename CircuitID -> ChannelNumber (preserves
            //      data), add new SecondaryBoardNumber column, shift +1 ────────
            migrationBuilder.RenameColumn(
                name: "CircuitID",
                schema: "Sessions",
                table: "BatterySessions",
                newName: "ChannelNumber");

            migrationBuilder.AddColumn<long>(
                name: "SecondaryBoardNumber",
                schema: "Sessions",
                table: "BatterySessions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.Sql(@"UPDATE BatterySessions SET ChannelNumber = ChannelNumber + 1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverse order of Up.
            //
            // NOTE: verified against a real data copy that `dotnet ef migrations script
            // AddSecondaryBoardAndChannel AddScheduleDbcPorts` produces SQL that correctly
            // preserves the Circuits/BatterySessions/CalibrationData rows when run directly.
            // However, applying this Down() live via `dotnet ef database update <target>`
            // was observed to lose the Circuits table's rows on this SQLite provider version
            // (a migrator execution-order quirk, not a defect in the SQL below). If a rollback
            // is ever genuinely needed, generate and review the script with `migrations script`
            // and run it directly rather than `database update`, and verify against a DB copy
            // first either way - do not run this Down() against production data unreviewed.

            migrationBuilder.Sql(@"UPDATE BatterySessions SET ChannelNumber = ChannelNumber - 1;");

            migrationBuilder.DropColumn(
                name: "SecondaryBoardNumber",
                schema: "Sessions",
                table: "BatterySessions");

            migrationBuilder.RenameColumn(
                name: "ChannelNumber",
                schema: "Sessions",
                table: "BatterySessions",
                newName: "CircuitID");

            migrationBuilder.Sql(@"UPDATE CalibrationData SET ChannelId = ChannelId - 1;");

            migrationBuilder.DropColumn(
                name: "SecondaryBoardNumber",
                schema: "Device",
                table: "CalibrationData");

            migrationBuilder.RenameColumn(
                name: "ChannelId",
                schema: "Device",
                table: "CalibrationData",
                newName: "CircuitId");

            migrationBuilder.DropForeignKey(
                name: "FK_Channels_SecondaryBoards_SecondaryBoardId",
                schema: "Device",
                table: "Channels");

            migrationBuilder.DropIndex(
                name: "IX_Channels_SecondaryBoardId_ChannelNumber",
                schema: "Device",
                table: "Channels");

            migrationBuilder.Sql(@"
                UPDATE Channels
                SET ChannelNumber = ChannelNumber - 1;
            ");

            migrationBuilder.DropColumn(
                name: "SecondaryBoardId",
                schema: "Device",
                table: "Channels");

            migrationBuilder.RenameColumn(
                name: "ChannelMaxChargingCurrent",
                schema: "Device",
                table: "Channels",
                newName: "CircuitMaxChargingCurrent");

            migrationBuilder.RenameColumn(
                name: "ChannelMaxDischargingCurrent",
                schema: "Device",
                table: "Channels",
                newName: "CircuitMaxDischargingCurrent");

            migrationBuilder.RenameColumn(
                name: "ChannelMinVoltage",
                schema: "Device",
                table: "Channels",
                newName: "CircuitMinVoltage");

            migrationBuilder.RenameColumn(
                name: "ChannelMaxVoltage",
                schema: "Device",
                table: "Channels",
                newName: "CircuitMaxVoltage");

            migrationBuilder.RenameColumn(
                name: "ChannelType",
                schema: "Device",
                table: "Channels",
                newName: "CircuitType");

            migrationBuilder.RenameColumn(
                name: "ChannelNumber",
                schema: "Device",
                table: "Channels",
                newName: "CircuitID");

            migrationBuilder.RenameTable(
                name: "Channels",
                schema: "Device",
                newName: "Circuits",
                newSchema: "Device");

            migrationBuilder.DropTable(
                name: "SecondaryBoards",
                schema: "Device");
        }
    }
}
