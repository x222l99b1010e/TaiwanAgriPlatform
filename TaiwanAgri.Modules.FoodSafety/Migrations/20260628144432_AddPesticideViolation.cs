using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaiwanAgri.Modules.FoodSafety.Migrations
{
    /// <inheritdoc />
    public partial class AddPesticideViolation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "foodsafety");

            migrationBuilder.CreateTable(
                name: "PesticideViolations",
                schema: "foodsafety",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Number = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SamplingDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ProductName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ProductId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ProducerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SamplingLocation = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    InspectResult = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SyncedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PesticideViolations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PesticideViolations_Number",
                schema: "foodsafety",
                table: "PesticideViolations",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PesticideViolations_SamplingDate",
                schema: "foodsafety",
                table: "PesticideViolations",
                column: "SamplingDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PesticideViolations",
                schema: "foodsafety");
        }
    }
}
