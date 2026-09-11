using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ambc.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddGameReferees : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GameReferees",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    RefereeId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AssignedBy = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameReferees", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GameReferees_GameId_RefereeId",
                table: "GameReferees",
                columns: new[] { "GameId", "RefereeId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GameReferees");
        }
    }
}
