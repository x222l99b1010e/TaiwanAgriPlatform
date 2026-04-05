using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaiwanAgri.Modules.Weather.Data.Migrations
{
    /// <inheritdoc />
    public partial class CreateWeatherObservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WeatherObsevations",
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WeatherObsevations");
        }
    }
}
