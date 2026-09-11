using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ambc.Api.Migrations
{
    /// <inheritdoc />
    public partial class RosterUniqueChange : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Rosters_SeasonId_TeamId_PlayerId",
                table: "Rosters");

            migrationBuilder.CreateIndex(
                name: "IX_Rosters_SeasonId_PlayerId",
                table: "Rosters",
                columns: new[] { "SeasonId", "PlayerId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Rosters_SeasonId_PlayerId",
                table: "Rosters");

            migrationBuilder.CreateIndex(
                name: "IX_Rosters_SeasonId_TeamId_PlayerId",
                table: "Rosters",
                columns: new[] { "SeasonId", "TeamId", "PlayerId" },
                unique: true);
        }
    }
}
