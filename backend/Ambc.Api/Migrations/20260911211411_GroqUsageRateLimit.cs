using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ambc.Api.Migrations
{
    /// <inheritdoc />
    public partial class GroqUsageRateLimit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GroqUsages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    Count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroqUsages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GroqUsages_UserId_Fecha",
                table: "GroqUsages",
                columns: new[] { "UserId", "Fecha" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GroqUsages");
        }
    }
}
