using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaiwanAgri.Modules.Market.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAgriProductsTransTcTypeIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_AgriProductsTrans_TcType",
                schema: "market",
                table: "AgriProductsTrans",
                column: "TcType");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AgriProductsTrans_TcType",
                schema: "market",
                table: "AgriProductsTrans");
        }
    }
}
