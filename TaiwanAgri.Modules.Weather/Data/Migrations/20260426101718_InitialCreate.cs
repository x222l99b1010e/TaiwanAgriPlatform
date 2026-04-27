using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaiwanAgri.Modules.Weather.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "weather");

            migrationBuilder.CreateTable(
                name: "PestAlerts",
                schema: "weather",
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
                name: "PestDecadeSummaries",
                schema: "weather",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PestName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    TenDays = table.Column<int>(type: "int", nullable: false),
                    City = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Town = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Average = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    ProportionIsland = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PestDecadeSummaries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PestRuleConfigs",
                schema: "weather",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    RuleName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RuleType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SourceTable = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    ExpiryDays = table.Column<int>(type: "int", nullable: false),
                    Threshold = table.Column<int>(type: "int", nullable: true),
                    FilterJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PestRuleConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RainfallObservations",
                schema: "weather",
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
                schema: "weather",
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

            migrationBuilder.CreateTable(
                name: "WeatherObservations",
                schema: "weather",
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
                schema: "weather",
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
                        principalSchema: "weather",
                        principalTable: "PestAlerts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PestAlertCrops",
                schema: "weather",
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
                        principalSchema: "weather",
                        principalTable: "PestAlerts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserNotifications",
                schema: "weather",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    PestRuleConfigId = table.Column<int>(type: "int", nullable: false),
                    SourceRecordId = table.Column<int>(type: "int", nullable: true),
                    Message = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    TriggeredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpireAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsRead = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserNotifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserNotifications_PestRuleConfigs_PestRuleConfigId",
                        column: x => x.PestRuleConfigId,
                        principalSchema: "weather",
                        principalTable: "PestRuleConfigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PestAlertCities_AlertId",
                schema: "weather",
                table: "PestAlertCities",
                column: "AlertId");

            migrationBuilder.CreateIndex(
                name: "IX_PestAlertCities_CityName",
                schema: "weather",
                table: "PestAlertCities",
                column: "CityName");

            migrationBuilder.CreateIndex(
                name: "IX_PestAlertCrops_AlertId",
                schema: "weather",
                table: "PestAlertCrops",
                column: "AlertId");

            migrationBuilder.CreateIndex(
                name: "IX_PestAlertCrops_CropName",
                schema: "weather",
                table: "PestAlertCrops",
                column: "CropName");

            migrationBuilder.CreateIndex(
                name: "IX_PestAlerts_SourceHash",
                schema: "weather",
                table: "PestAlerts",
                column: "SourceHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PestDecadeSummaries_Unique",
                schema: "weather",
                table: "PestDecadeSummaries",
                columns: new[] { "PestName", "Year", "Month", "TenDays", "City", "Town" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PestRuleConfigs_RuleName",
                schema: "weather",
                table: "PestRuleConfigs",
                column: "RuleName");

            migrationBuilder.CreateIndex(
                name: "IX_PestRuleConfigs_UserId_IsActive",
                schema: "weather",
                table: "PestRuleConfigs",
                columns: new[] { "UserId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_RainfallObservations_StationId_ObservedAt",
                schema: "weather",
                table: "RainfallObservations",
                columns: new[] { "StationId", "ObservedAt" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RainfallStations_StationId",
                schema: "weather",
                table: "RainfallStations",
                column: "StationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserNotifications_PestRuleConfigId",
                schema: "weather",
                table: "UserNotifications",
                column: "PestRuleConfigId");

            migrationBuilder.CreateIndex(
                name: "IX_UserNotifications_UserId_IsRead",
                schema: "weather",
                table: "UserNotifications",
                columns: new[] { "UserId", "IsRead" });

            migrationBuilder.CreateIndex(
                name: "IX_WeatherObservations_CityCode_ObservedAt",
                schema: "weather",
                table: "WeatherObservations",
                columns: new[] { "CityCode", "ObservedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WeatherObservations_StationId",
                schema: "weather",
                table: "WeatherObservations",
                column: "StationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PestAlertCities",
                schema: "weather");

            migrationBuilder.DropTable(
                name: "PestAlertCrops",
                schema: "weather");

            migrationBuilder.DropTable(
                name: "PestDecadeSummaries",
                schema: "weather");

            migrationBuilder.DropTable(
                name: "RainfallObservations",
                schema: "weather");

            migrationBuilder.DropTable(
                name: "RainfallStations",
                schema: "weather");

            migrationBuilder.DropTable(
                name: "UserNotifications",
                schema: "weather");

            migrationBuilder.DropTable(
                name: "WeatherObservations",
                schema: "weather");

            migrationBuilder.DropTable(
                name: "PestAlerts",
                schema: "weather");

            migrationBuilder.DropTable(
                name: "PestRuleConfigs",
                schema: "weather");
        }
    }
}
