using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaiwanAgri.Modules.Weather.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRainfallTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RainfallObservations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StationId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ObservedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Rain = table.Column<decimal>(type: "decimal(6,2)", nullable: true),
                    Min10 = table.Column<decimal>(type: "decimal(6,2)", nullable: true),
                    Hour3 = table.Column<decimal>(type: "decimal(6,2)", nullable: true),
                    Hour6 = table.Column<decimal>(type: "decimal(6,2)", nullable: true),
                    Hour12 = table.Column<decimal>(type: "decimal(6,2)", nullable: true),
                    Hour24 = table.Column<decimal>(type: "decimal(6,2)", nullable: true),
                    Now = table.Column<decimal>(type: "decimal(6,2)", nullable: true),
                    Attribute = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SyncedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RainfallObservations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RainfallStations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StationId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StationName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Latitude = table.Column<decimal>(type: "decimal(10,6)", nullable: true),
                    Longitude = table.Column<decimal>(type: "decimal(10,6)", nullable: true),
                    Elevation = table.Column<int>(type: "int", nullable: true),
                    CityName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CityCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TownName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TownCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RainfallStations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RainfallObservations_StationId_ObservedAt",
                table: "RainfallObservations",
                columns: new[] { "StationId", "ObservedAt" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RainfallStations_StationId",
                table: "RainfallStations",
                column: "StationId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RainfallObservations");

            migrationBuilder.DropTable(
                name: "RainfallStations");
        }
    }
}
