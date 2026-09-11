using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaiwanAgri.Modules.User.Migrations
{
    /// <inheritdoc />
    public partial class W26_WatchlistLogicalFk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserWatchlists_UserFarmProfiles_UserId",
                table: "UserWatchlists");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddForeignKey(
                name: "FK_UserWatchlists_UserFarmProfiles_UserId",
                table: "UserWatchlists",
                column: "UserId",
                principalTable: "UserFarmProfiles",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
