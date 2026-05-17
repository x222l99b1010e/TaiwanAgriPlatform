using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaiwanAgri.Core.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNavModuleAndRoleModulePermission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NavModules",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Route = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Icon = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    ParentId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NavModules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NavModules_NavModules_ParentId",
                        column: x => x.ParentId,
                        principalSchema: "core",
                        principalTable: "NavModules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RoleModulePermissions",
                schema: "core",
                columns: table => new
                {
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ModuleId = table.Column<int>(type: "int", nullable: false),
                    CanView = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleModulePermissions", x => new { x.RoleId, x.ModuleId });
                    table.ForeignKey(
                        name: "FK_RoleModulePermissions_NavModules_ModuleId",
                        column: x => x.ModuleId,
                        principalSchema: "core",
                        principalTable: "NavModules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NavModules_ParentId",
                schema: "core",
                table: "NavModules",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_RoleModulePermissions_ModuleId",
                schema: "core",
                table: "RoleModulePermissions",
                column: "ModuleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RoleModulePermissions",
                schema: "core");

            migrationBuilder.DropTable(
                name: "NavModules",
                schema: "core");
        }
    }
}
