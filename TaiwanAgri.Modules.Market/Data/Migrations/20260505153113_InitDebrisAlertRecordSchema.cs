using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaiwanAgri.Modules.Market.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitDebrisAlertRecordSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DebrisAlertRecords",
                schema: "market",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DisasterID = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DisasterName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AlertType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DebrisNo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LandslideID = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LandslideName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    County = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Town = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Vill = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AlertLevel = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    LastUpdateDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReportID = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    CountyCode = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AreaCode = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DebrisAlertRecords", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DebrisAlertRecords_ReportID_DebrisNo_LandslideID",
                schema: "market",
                table: "DebrisAlertRecords",
                columns: new[] { "ReportID", "DebrisNo", "LandslideID" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DebrisAlertRecords",
                schema: "market");
        }
    }
}
