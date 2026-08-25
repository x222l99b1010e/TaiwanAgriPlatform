using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaiwanAgri.Modules.Market.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPoultryTrans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PoultryTrans",
                schema: "market",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TransDate = table.Column<DateOnly>(type: "date", nullable: false),
                    MetricCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Price = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: true),
                    PriceStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RawValue = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SyncedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PoultryTrans", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PoultryTrans_TransDate_MetricCode",
                schema: "market",
                table: "PoultryTrans",
                columns: new[] { "TransDate", "MetricCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PoultryTrans",
                schema: "market");
        }
    }
}
