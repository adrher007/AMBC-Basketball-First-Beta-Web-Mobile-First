using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ambc.Api.Migrations
{
    /// <inheritdoc />
    public partial class ProgramacionSemanalYNotificaciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Semana",
                table: "Games",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "NotificationEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tipo = table.Column<string>(type: "text", nullable: false),
                    Fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    NotificationId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreadoEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationEvents_GameId_Tipo_Fecha",
                table: "NotificationEvents",
                columns: new[] { "GameId", "Tipo", "Fecha" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotificationEvents");

            migrationBuilder.DropColumn(
                name: "Semana",
                table: "Games");
        }
    }
}
