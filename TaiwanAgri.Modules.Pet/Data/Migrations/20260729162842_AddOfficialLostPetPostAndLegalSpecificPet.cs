using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaiwanAgri.Modules.Pet.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOfficialLostPetPostAndLegalSpecificPet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LegalSpecificPets",
                schema: "pet",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExternalId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    County = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BusinessItems = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    AnimalType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    PermitNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PermitValidDate = table.Column<DateOnly>(type: "date", nullable: true),
                    OwnerName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ResponsibleStaffName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RankYear = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    RankGrade = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RankDataConfirmed = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RankDescriptionConfirmed = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RankText = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    StateFlag = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SyncedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegalSpecificPets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OfficialLostPetPosts",
                schema: "pet",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KeyNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ChipNum = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    PetName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Sex = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Variety = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Coat = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Exterior = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Feature = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    LostTime = table.Column<DateOnly>(type: "date", nullable: false),
                    LostPlace = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    FeederName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PhoneNum = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    EMail = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PictureUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SyncedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OfficialLostPetPosts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LegalSpecificPets_ExternalId",
                schema: "pet",
                table: "LegalSpecificPets",
                column: "ExternalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OfficialLostPetPosts_KeyNo",
                schema: "pet",
                table: "OfficialLostPetPosts",
                column: "KeyNo",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LegalSpecificPets",
                schema: "pet");

            migrationBuilder.DropTable(
                name: "OfficialLostPetPosts",
                schema: "pet");
        }
    }
}
