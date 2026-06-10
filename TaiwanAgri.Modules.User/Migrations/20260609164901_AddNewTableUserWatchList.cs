using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaiwanAgri.Modules.User.Migrations
{
    /// <inheritdoc />
    public partial class AddNewTableUserWatchList : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserWatchlists",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CropCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CropName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    MarketCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    MarketName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserWatchlists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserWatchlists_UserFarmProfiles_UserId",
                        column: x => x.UserId,
                        principalTable: "UserFarmProfiles",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserWatchlists_UserId",
                table: "UserWatchlists",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserWatchlists");
        }
    }
}
