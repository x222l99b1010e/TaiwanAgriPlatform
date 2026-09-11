using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaiwanAgri.Modules.Weather.Data.Migrations
{
    /// <inheritdoc />
    public partial class W26_NotificationRuleFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FilterJson",
                schema: "weather",
                table: "PestRuleConfigs");

            migrationBuilder.AlterColumn<decimal>(
                name: "Threshold",
                schema: "weather",
                table: "PestRuleConfigs",
                type: "decimal(4,1)",
                precision: 4,
                scale: 1,
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Comparison",
                schema: "weather",
                table: "PestRuleConfigs",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FilterCity",
                schema: "weather",
                table: "PestRuleConfigs",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "FilterDateFrom",
                schema: "weather",
                table: "PestRuleConfigs",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FilterPlantName",
                schema: "weather",
                table: "PestRuleConfigs",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastEvaluatedAt",
                schema: "weather",
                table: "PestRuleConfigs",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MetricName",
                schema: "weather",
                table: "PestRuleConfigs",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeatherObservations_SyncedAt",
                schema: "weather",
                table: "WeatherObservations",
                column: "SyncedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WeatherObservations_SyncedAt",
                schema: "weather",
                table: "WeatherObservations");

            migrationBuilder.DropColumn(
                name: "Comparison",
                schema: "weather",
                table: "PestRuleConfigs");

            migrationBuilder.DropColumn(
                name: "FilterCity",
                schema: "weather",
                table: "PestRuleConfigs");

            migrationBuilder.DropColumn(
                name: "FilterDateFrom",
                schema: "weather",
                table: "PestRuleConfigs");

            migrationBuilder.DropColumn(
                name: "FilterPlantName",
                schema: "weather",
                table: "PestRuleConfigs");

            migrationBuilder.DropColumn(
                name: "LastEvaluatedAt",
                schema: "weather",
                table: "PestRuleConfigs");

            migrationBuilder.DropColumn(
                name: "MetricName",
                schema: "weather",
                table: "PestRuleConfigs");

            migrationBuilder.AlterColumn<int>(
                name: "Threshold",
                schema: "weather",
                table: "PestRuleConfigs",
                type: "int",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(4,1)",
                oldPrecision: 4,
                oldScale: 1,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FilterJson",
                schema: "weather",
                table: "PestRuleConfigs",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
