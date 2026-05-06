using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaiwanAgri.Modules.Market.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitPorkTransSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PorkTrans",
                schema: "market",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TransDate = table.Column<DateOnly>(type: "date", nullable: false),
                    MarketName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TotalTransCount = table.Column<int>(type: "int", nullable: false),
                    TotalTransAvgWeight = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    TotalTransAvgPrice = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    SpecPigCount = table.Column<int>(type: "int", nullable: false),
                    SpecPigAvgWeight = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    SpecPigAvgPrice = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    Count95To115kg = table.Column<int>(type: "int", nullable: false),
                    AvgWeight95To115kg = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    AvgPrice95To115kg = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    Count75To95kg = table.Column<int>(type: "int", nullable: false),
                    AvgWeight75To95kg = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    AvgPrice75To95kg = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    Count115To135kg = table.Column<int>(type: "int", nullable: false),
                    AvgWeight115To135kg = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    AvgPrice115To135kg = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    CountUnder75kg = table.Column<int>(type: "int", nullable: false),
                    AvgWeightUnder75kg = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    AvgPriceUnder75kg = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    OutPigsCount = table.Column<int>(type: "int", nullable: false),
                    OutPigsAvgWeight = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    OutPigsAvgPrice = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    OtherPigsCount = table.Column<int>(type: "int", nullable: false),
                    OtherPigsAvgWeight = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    OtherPigsAvgPrice = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    FreezerPigsCount = table.Column<int>(type: "int", nullable: false),
                    FreezerPigsAvgWeight = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    FreezerPigsAvgPrice = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    ExcludeFreezerCount = table.Column<int>(type: "int", nullable: false),
                    ExcludeFreezerAvgWeight = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    ExcludeFreezerAvgPrice = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    Count135To155kg = table.Column<int>(type: "int", nullable: false),
                    AvgWeight135To155kg = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    AvgPrice135To155kg = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    CountAbove155kg = table.Column<int>(type: "int", nullable: false),
                    AvgWeightAbove155kg = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    AvgPriceAbove155kg = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PorkTrans", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PorkTrans_TransDate_MarketName",
                schema: "market",
                table: "PorkTrans",
                columns: new[] { "TransDate", "MarketName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PorkTrans",
                schema: "market");
        }
    }
}
