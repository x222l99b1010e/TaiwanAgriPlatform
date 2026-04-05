using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaiwanAgri.Modules.Weather.Data.Migrations
{
    /// <inheritdoc />
    public partial class CreatePestAlerts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WeatherObsevations");

            migrationBuilder.CreateTable(
                name: "PestAlerts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Subject = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Prescription = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CitiesRaw = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    PlantNamesRaw = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PubDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Issue = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SourceHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SyncedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PestAlerts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WeatherObservations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StationId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StationName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ObservedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Latitude = table.Column<decimal>(type: "decimal(10,6)", nullable: true),
                    Longitude = table.Column<decimal>(type: "decimal(10,6)", nullable: true),
                    Elevation = table.Column<int>(type: "int", nullable: true),
                    WindDirection = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    WindSpeed = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    MaxGust = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    MaxGustDirection = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Temperature = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    Humidity = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    Pressure = table.Column<decimal>(type: "decimal(7,3)", nullable: true),
                    SunshineHours = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    Rainfall24h = table.Column<decimal>(type: "decimal(6,2)", nullable: true),
                    DailyMaxTemp = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    DailyMinTemp = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    CityCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CityName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TownCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    TownName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SyncedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeatherObservations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PestAlertCities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AlertId = table.Column<int>(type: "int", nullable: false),
                    CityName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PestAlertCities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PestAlertCities_PestAlerts_AlertId",
                        column: x => x.AlertId,
                        principalTable: "PestAlerts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PestAlertCrops",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AlertId = table.Column<int>(type: "int", nullable: false),
                    CropName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PestAlertCrops", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PestAlertCrops_PestAlerts_AlertId",
                        column: x => x.AlertId,
                        principalTable: "PestAlerts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PestAlertCities_AlertId",
                table: "PestAlertCities",
                column: "AlertId");

            migrationBuilder.CreateIndex(
                name: "IX_PestAlertCities_CityName",
                table: "PestAlertCities",
                column: "CityName");

            migrationBuilder.CreateIndex(
                name: "IX_PestAlertCrops_AlertId",
                table: "PestAlertCrops",
                column: "AlertId");

            migrationBuilder.CreateIndex(
                name: "IX_PestAlertCrops_CropName",
                table: "PestAlertCrops",
                column: "CropName");

            migrationBuilder.CreateIndex(
                name: "IX_PestAlerts_SourceHash",
                table: "PestAlerts",
                column: "SourceHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeatherObservations_CityCode_ObservedAt",
                table: "WeatherObservations",
                columns: new[] { "CityCode", "ObservedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WeatherObservations_StationId",
                table: "WeatherObservations",
                column: "StationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PestAlertCities");

            migrationBuilder.DropTable(
                name: "PestAlertCrops");

            migrationBuilder.DropTable(
                name: "WeatherObservations");

            migrationBuilder.DropTable(
                name: "PestAlerts");

            migrationBuilder.CreateTable(
                name: "WeatherObsevations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CityCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CityName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DailyMaxTemp = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    DailyMinTemp = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    Elevation = table.Column<int>(type: "int", nullable: true),
                    Humidity = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    Latitude = table.Column<decimal>(type: "decimal(10,6)", nullable: true),
                    Longitude = table.Column<decimal>(type: "decimal(10,6)", nullable: true),
                    MaxGust = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    MaxGustDirection = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    ObservedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Pressure = table.Column<decimal>(type: "decimal(7,3)", nullable: true),
                    Rainfall24h = table.Column<decimal>(type: "decimal(6,2)", nullable: true),
                    StationId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StationName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SunshineHours = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    SyncedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Temperature = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    TownCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    TownName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    WindDirection = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    WindSpeed = table.Column<decimal>(type: "decimal(5,2)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeatherObsevations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WeatherObservations_CityCode_ObservedAt",
                table: "WeatherObsevations",
                columns: new[] { "CityCode", "ObservedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WeatherObservations_StationId",
                table: "WeatherObsevations",
                column: "StationId");
        }
    }
}
