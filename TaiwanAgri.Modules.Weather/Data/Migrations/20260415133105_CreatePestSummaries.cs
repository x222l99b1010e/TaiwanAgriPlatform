using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaiwanAgri.Modules.Weather.Data.Migrations
{
    /// <inheritdoc />
    public partial class CreatePestSummaries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PestDecadeSummaries",
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
                    Average = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    ProportionIsland = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PestDecadeSummaries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PestDecadeSummaries_Unique",
                table: "PestDecadeSummaries",
                columns: new[] { "PestName", "Year", "Month", "TenDays", "City", "Town" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PestDecadeSummaries");
        }
    }
}
