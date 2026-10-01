using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BatteryTestingSystem.Migrations
{
    /// <inheritdoc />
    public partial class TableOP : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_BatterySessions",
                schema: "Sessions",
                table: "BatterySessions");

            migrationBuilder.RenameColumn(
                name: "Id",
                schema: "Sessions",
                table: "BatterySessions",
                newName: "ProgramID");

            migrationBuilder.AddColumn<bool>(
                name: "IsVaild",
                schema: "Program",
                table: "Programs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<long>(
                name: "SessionID",
                schema: "Sessions",
                table: "BatterySessions",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "INTEGER")
                .Annotation("Sqlite:Autoincrement", true);

            migrationBuilder.AlterColumn<long>(
                name: "ProgramID",
                schema: "Sessions",
                table: "BatterySessions",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "INTEGER")
                .OldAnnotation("Sqlite:Autoincrement", true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EndTime",
                schema: "Sessions",
                table: "BatterySessions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SessionName",
                schema: "Sessions",
                table: "BatterySessions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartTime",
                schema: "Sessions",
                table: "BatterySessions",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddPrimaryKey(
                name: "PK_BatterySessions",
                schema: "Sessions",
                table: "BatterySessions",
                column: "SessionID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_BatterySessions",
                schema: "Sessions",
                table: "BatterySessions");

            migrationBuilder.DropColumn(
                name: "IsVaild",
                schema: "Program",
                table: "Programs");

            migrationBuilder.DropColumn(
                name: "EndTime",
                schema: "Sessions",
                table: "BatterySessions");

            migrationBuilder.DropColumn(
                name: "SessionName",
                schema: "Sessions",
                table: "BatterySessions");

            migrationBuilder.DropColumn(
                name: "StartTime",
                schema: "Sessions",
                table: "BatterySessions");

            migrationBuilder.RenameColumn(
                name: "ProgramID",
                schema: "Sessions",
                table: "BatterySessions",
                newName: "Id");

            migrationBuilder.AlterColumn<long>(
                name: "SessionID",
                schema: "Sessions",
                table: "BatterySessions",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "INTEGER")
                .OldAnnotation("Sqlite:Autoincrement", true);

            migrationBuilder.AlterColumn<long>(
                name: "Id",
                schema: "Sessions",
                table: "BatterySessions",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "INTEGER")
                .Annotation("Sqlite:Autoincrement", true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_BatterySessions",
                schema: "Sessions",
                table: "BatterySessions",
                column: "Id");
        }
    }
}
