using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ambc.Api.Migrations
{
    /// <inheritdoc />
    public partial class RowVersionPlanillaYGame : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Planillas",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Games",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            // PostgreSQL no tiene rowversion nativo (como SQL Server): la columna se
            // actualiza con un trigger en cada UPDATE y con un DEFAULT en cada INSERT.
            // EF la usa como token de concurrencia (WHERE RowVersion = @original).
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION ambc_set_rowversion() RETURNS trigger AS $$
                BEGIN
                    NEW."RowVersion" := decode(md5(random()::text || clock_timestamp()::text), 'hex');
                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;
                """);

            foreach (var tabla in new[] { "Games", "Planillas" })
            {
                migrationBuilder.Sql($"""
                    ALTER TABLE "{tabla}" ALTER COLUMN "RowVersion"
                    SET DEFAULT decode(md5(random()::text || clock_timestamp()::text), 'hex');
                    """);

                migrationBuilder.Sql($"""
                    CREATE TRIGGER "trg_{tabla}_rowversion"
                    BEFORE UPDATE ON "{tabla}"
                    FOR EACH ROW EXECUTE FUNCTION ambc_set_rowversion();
                    """);

                // Valores iniciales distintos por fila (evita tokens idénticos/vacíos)
                migrationBuilder.Sql($"""
                    UPDATE "{tabla}"
                    SET "RowVersion" = decode(md5(random()::text || clock_timestamp()::text || "Id"::text), 'hex');
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var tabla in new[] { "Games", "Planillas" })
            {
                migrationBuilder.Sql($"""DROP TRIGGER IF EXISTS "trg_{tabla}_rowversion" ON "{tabla}";""");
            }
            migrationBuilder.Sql("""DROP FUNCTION IF EXISTS ambc_set_rowversion();""");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Planillas");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Games");
        }
    }
}
