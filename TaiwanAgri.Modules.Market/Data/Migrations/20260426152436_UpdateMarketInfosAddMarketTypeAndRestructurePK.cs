using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaiwanAgri.Modules.Market.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdateMarketInfosAddMarketTypeAndRestructurePK : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AgriProductsTrans_MarketInfos_MarketCode",
                schema: "market",
                table: "AgriProductsTrans");

            migrationBuilder.DropPrimaryKey(
                name: "PK_MarketInfos",
                schema: "market",
                table: "MarketInfos");

            migrationBuilder.AddColumn<int>(
                name: "Id",
                schema: "market",
                table: "MarketInfos",
                type: "int",
                nullable: false,
                defaultValue: 0)
                .Annotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AddColumn<string>(
                name: "MarketType",
                schema: "market",
                table: "MarketInfos",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "PK_MarketInfos",
                schema: "market",
                table: "MarketInfos",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_MarketInfos_MarketCode_MarketName",
                schema: "market",
                table: "MarketInfos",
                columns: new[] { "MarketCode", "MarketName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_MarketInfos",
                schema: "market",
                table: "MarketInfos");

            migrationBuilder.DropIndex(
                name: "IX_MarketInfos_MarketCode_MarketName",
                schema: "market",
                table: "MarketInfos");

            migrationBuilder.DropColumn(
                name: "Id",
                schema: "market",
                table: "MarketInfos");

            migrationBuilder.DropColumn(
                name: "MarketType",
                schema: "market",
                table: "MarketInfos");

            migrationBuilder.AddPrimaryKey(
                name: "PK_MarketInfos",
                schema: "market",
                table: "MarketInfos",
                column: "MarketCode");

            migrationBuilder.AddForeignKey(
                name: "FK_AgriProductsTrans_MarketInfos_MarketCode",
                schema: "market",
                table: "AgriProductsTrans",
                column: "MarketCode",
                principalSchema: "market",
                principalTable: "MarketInfos",
                principalColumn: "MarketCode",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
