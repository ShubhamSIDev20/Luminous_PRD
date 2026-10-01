using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BatteryTestingSystem.Migrations
{
    /// <inheritdoc />
    public partial class RegistrationStandards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "Programs");

            migrationBuilder.CreateTable(
                name: "Standards",
                schema: "Programs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    StandardName = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    UnitList = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Standards", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Standards",
                schema: "Programs");
        }
    }
}
