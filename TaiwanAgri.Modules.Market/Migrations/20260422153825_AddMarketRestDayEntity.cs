using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaiwanAgri.Modules.Market.Migrations
{
    /// <inheritdoc />
    public partial class AddMarketRestDayEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MarketRestDays",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MarketCode = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    MarketName = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    MarketType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    RestDay = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketRestDays", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MarketRestDays_MarketCode_MarketType_Year_Month_RestDay",
                table: "MarketRestDays",
                columns: new[] { "MarketCode", "MarketType", "Year", "Month", "RestDay" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MarketRestDays");
        }
    }
}
