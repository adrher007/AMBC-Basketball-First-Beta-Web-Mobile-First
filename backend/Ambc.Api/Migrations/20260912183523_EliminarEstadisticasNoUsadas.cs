using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ambc.Api.Migrations
{
    /// <inheritdoc />
    public partial class EliminarEstadisticasNoUsadas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Ast",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "Blk",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "Reb",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "Stl",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "To",
                table: "PlayerGameStats");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Ast",
                table: "PlayerGameStats",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Blk",
                table: "PlayerGameStats",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Reb",
                table: "PlayerGameStats",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Stl",
                table: "PlayerGameStats",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "To",
                table: "PlayerGameStats",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
