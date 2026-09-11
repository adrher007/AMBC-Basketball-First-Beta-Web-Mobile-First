using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ambc.Api.Migrations
{
    /// <inheritdoc />
    public partial class NotificationEventPorArbitro : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_NotificationEvents_GameId_Tipo_Fecha",
                table: "NotificationEvents");

            migrationBuilder.AddColumn<Guid>(
                name: "RefereeId",
                table: "NotificationEvents",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_NotificationEvents_GameId_Tipo_Fecha_RefereeId",
                table: "NotificationEvents",
                columns: new[] { "GameId", "Tipo", "Fecha", "RefereeId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_NotificationEvents_GameId_Tipo_Fecha_RefereeId",
                table: "NotificationEvents");

            migrationBuilder.DropColumn(
                name: "RefereeId",
                table: "NotificationEvents");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationEvents_GameId_Tipo_Fecha",
                table: "NotificationEvents",
                columns: new[] { "GameId", "Tipo", "Fecha" },
                unique: true);
        }
    }
}
