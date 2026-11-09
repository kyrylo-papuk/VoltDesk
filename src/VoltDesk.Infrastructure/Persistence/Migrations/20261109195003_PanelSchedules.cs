using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VoltDesk.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PanelSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ElectricalPanels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Location = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    System = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    BusRatingAmps = table.Column<int>(type: "int", nullable: false),
                    MainBreakerAmps = table.Column<int>(type: "int", nullable: true),
                    Spaces = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ElectricalPanels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ElectricalPanels_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Circuits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PanelId = table.Column<int>(type: "int", nullable: false),
                    Slot = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LoadType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    LoadVa = table.Column<decimal>(type: "decimal(12,1)", precision: 12, scale: 1, nullable: false),
                    Poles = table.Column<int>(type: "int", nullable: false),
                    BreakerAmps = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Circuits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Circuits_ElectricalPanels_PanelId",
                        column: x => x.PanelId,
                        principalTable: "ElectricalPanels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Circuits_PanelId",
                table: "Circuits",
                column: "PanelId");

            migrationBuilder.CreateIndex(
                name: "IX_ElectricalPanels_ProjectId_Name",
                table: "ElectricalPanels",
                columns: new[] { "ProjectId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Circuits");

            migrationBuilder.DropTable(
                name: "ElectricalPanels");
        }
    }
}
