using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaiwanAgri.Modules.Market.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAgriProductsTransLatestPriceIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_AgriProductsTrans_CropCode_MarketCode_TransDate",
                schema: "market",
                table: "AgriProductsTrans",
                columns: new[] { "CropCode", "MarketCode", "TransDate" },
                descending: new[] { false, false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AgriProductsTrans_CropCode_MarketCode_TransDate",
                schema: "market",
                table: "AgriProductsTrans");
        }
    }
}
