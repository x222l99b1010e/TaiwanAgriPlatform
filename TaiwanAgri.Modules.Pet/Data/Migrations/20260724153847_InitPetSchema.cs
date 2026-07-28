using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaiwanAgri.Modules.Pet.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitPetSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "pet");

            migrationBuilder.CreateTable(
                name: "Shelters",
                schema: "pet",
                columns: table => new
                {
                    ShelterPkId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Tel = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    County = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Latitude = table.Column<decimal>(type: "decimal(10,6)", nullable: false),
                    Longitude = table.Column<decimal>(type: "decimal(10,6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shelters", x => x.ShelterPkId);
                });

            migrationBuilder.CreateTable(
                name: "ShelterAnimals",
                schema: "pet",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AnimalSubId = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    ShelterPkId = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Sex = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BodyType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Age = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Sterilization = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Bacterin = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Variety = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Colour = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FoundPlace = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Remark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    OpenDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedTime = table.Column<DateOnly>(type: "date", nullable: false),
                    SourceUpdatedAt = table.Column<DateOnly>(type: "date", nullable: true),
                    AlbumFile = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SyncedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShelterAnimals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShelterAnimals_Shelters_ShelterPkId",
                        column: x => x.ShelterPkId,
                        principalSchema: "pet",
                        principalTable: "Shelters",
                        principalColumn: "ShelterPkId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShelterAnimals_ShelterPkId_AnimalSubId",
                schema: "pet",
                table: "ShelterAnimals",
                columns: new[] { "ShelterPkId", "AnimalSubId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShelterAnimals",
                schema: "pet");

            migrationBuilder.DropTable(
                name: "Shelters",
                schema: "pet");
        }
    }
}
