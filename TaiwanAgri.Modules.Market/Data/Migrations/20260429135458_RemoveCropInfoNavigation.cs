using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaiwanAgri.Modules.Market.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveCropInfoNavigation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AgriProductsTrans_CropInfos_CropCode",
                schema: "market",
                table: "AgriProductsTrans");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddForeignKey(
                name: "FK_AgriProductsTrans_CropInfos_CropCode",
                schema: "market",
                table: "AgriProductsTrans",
                column: "CropCode",
                principalSchema: "market",
                principalTable: "CropInfos",
                principalColumn: "CropCode",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
