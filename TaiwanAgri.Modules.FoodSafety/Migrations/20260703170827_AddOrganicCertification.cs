using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaiwanAgri.Modules.FoodSafety.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganicCertification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OrganicCertifications",
                schema: "foodsafety",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CertOrganicSn = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Tel = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Products = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BehaviorType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CompanyName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ContainCrops = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MailingAddress = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    OldCertOrganicSN = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsMultiCertSource = table.Column<bool>(type: "bit", nullable: false),
                    SyncedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganicCertifications", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrganicCertifications_CertOrganicSn",
                schema: "foodsafety",
                table: "OrganicCertifications",
                column: "CertOrganicSn",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrganicCertifications",
                schema: "foodsafety");
        }
    }
}
