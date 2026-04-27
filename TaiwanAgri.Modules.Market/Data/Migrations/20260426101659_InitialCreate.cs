using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaiwanAgri.Modules.Market.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "market");

            migrationBuilder.CreateTable(
                name: "CropInfos",
                schema: "market",
                columns: table => new
                {
                    CropCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CropName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CropInfos", x => x.CropCode);
                });

            migrationBuilder.CreateTable(
                name: "MarketInfos",
                schema: "market",
                columns: table => new
                {
                    MarketCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MarketName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketInfos", x => x.MarketCode);
                });

            migrationBuilder.CreateTable(
                name: "MarketRestDays",
                schema: "market",
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

            migrationBuilder.CreateTable(
                name: "AgriProductsTrans",
                schema: "market",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TransDate = table.Column<DateOnly>(type: "date", nullable: false),
                    TcType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CropCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MarketCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    UpperPrice = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    MiddlePrice = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    LowerPrice = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    AvgPrice = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    TransQuantity = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgriProductsTrans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgriProductsTrans_CropInfos_CropCode",
                        column: x => x.CropCode,
                        principalSchema: "market",
                        principalTable: "CropInfos",
                        principalColumn: "CropCode",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AgriProductsTrans_MarketInfos_MarketCode",
                        column: x => x.MarketCode,
                        principalSchema: "market",
                        principalTable: "MarketInfos",
                        principalColumn: "MarketCode",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgriProductsTrans_CropCode_TransDate",
                schema: "market",
                table: "AgriProductsTrans",
                columns: new[] { "CropCode", "TransDate" });

            migrationBuilder.CreateIndex(
                name: "IX_AgriProductsTrans_MarketCode_TransDate",
                schema: "market",
                table: "AgriProductsTrans",
                columns: new[] { "MarketCode", "TransDate" });

            migrationBuilder.CreateIndex(
                name: "IX_AgriProductsTrans_TransDate_TcType_CropCode_MarketCode",
                schema: "market",
                table: "AgriProductsTrans",
                columns: new[] { "TransDate", "TcType", "CropCode", "MarketCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MarketRestDays_MarketCode_MarketType_Year_Month_RestDay",
                schema: "market",
                table: "MarketRestDays",
                columns: new[] { "MarketCode", "MarketType", "Year", "Month", "RestDay" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgriProductsTrans",
                schema: "market");

            migrationBuilder.DropTable(
                name: "MarketRestDays",
                schema: "market");

            migrationBuilder.DropTable(
                name: "CropInfos",
                schema: "market");

            migrationBuilder.DropTable(
                name: "MarketInfos",
                schema: "market");
        }
    }
}
