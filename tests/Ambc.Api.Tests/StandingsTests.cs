using Ambc.Api.Data;
using Ambc.Api.Models;
using Ambc.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Ambc.Api.Tests;

/// <summary>
/// StandingsCalculator: puntuación V=2/D=1 y desempates.
/// Nota: Recalcular sólo considera equipos CON roster en la temporada
/// (db.Rosters), por eso cada equipo de prueba se crea con plantilla.
/// </summary>
public class StandingsTests
{
    /// <summary>Equipo con 1 jugador en el roster de la temporada.</summary>
    private static Team Equipo(AmbcDbContext db, Season s, string nombre, string corto)
    {
        var t = TestDb.Team(db, nombre, corto);
        TestDb.Roster(db, s, t, numero: 1);
        return t;
    }

    private static async Task<List<Standing>> Tabla(AmbcDbContext db, Guid seasonId) =>
        await db.Standings.Where(s => s.SeasonId == seasonId).OrderBy(s => s.Posicion).ToListAsync();

    [Fact]
    public async Task Puntuacion_es_2_por_victoria_y_1_por_derrota()
    {
        var db = TestDb.Crear();
        var s = TestDb.Season(db, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        var a = Equipo(db, s, "A", "AAA");
        var b = Equipo(db, s, "B", "BBB");
        var c = Equipo(db, s, "C", "CCC");

        // A gana a B, B gana a C, A gana a C → A: 2V, B: 1V1D, C: 2D
        TestDb.Game(db, s, a, b, new DateOnly(2026, 1, 5), 80, 70, GameEstado.FINISHED);
        TestDb.Game(db, s, b, c, new DateOnly(2026, 1, 6), 80, 70, GameEstado.FINISHED);
        TestDb.Game(db, s, c, a, new DateOnly(2026, 1, 7), 70, 80, GameEstado.FINISHED);

        await StandingsCalculator.Recalcular(db, s.Id);
        var tabla = await Tabla(db, s.Id);

        var ra = tabla.Single(t => t.TeamId == a.Id);
        var rb = tabla.Single(t => t.TeamId == b.Id);
        var rc = tabla.Single(t => t.TeamId == c.Id);

        Assert.Equal(2, ra.Victorias);
        Assert.Equal(0, ra.Derrotas);
        Assert.Equal(4, ra.Victorias * 2 + ra.Derrotas);   // 2×2 + 0
        Assert.Equal(3, rb.Victorias * 2 + rb.Derrotas);   // 1×2 + 1
        Assert.Equal(2, rc.Victorias * 2 + rc.Derrotas);   // 0×2 + 2

        Assert.Equal(1, ra.Posicion);
        Assert.Equal(2, rb.Posicion);
        Assert.Equal(3, rc.Posicion);
    }

    [Fact]
    public async Task Desempate_entre_dos_equipos_usa_el_enfrentamiento_directo()
    {
        var db = TestDb.Crear();
        var s = TestDb.Season(db, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        var a = Equipo(db, s, "A", "AAA");
        var b = Equipo(db, s, "B", "BBB");
        var c = Equipo(db, s, "C", "CCC");
        var d = Equipo(db, s, "D", "DDD");

        // A: 2V (4) | B: 1V1D (3) | C: 1V1D (3) | D: 2D (2)
        // B y C EMPATAN en puntos y B venció a C en el enfrentamiento directo → B arriba.
        TestDb.Game(db, s, a, b, new DateOnly(2026, 1, 5), 80, 70, GameEstado.FINISHED);
        TestDb.Game(db, s, b, c, new DateOnly(2026, 1, 6), 80, 70, GameEstado.FINISHED);
        TestDb.Game(db, s, a, d, new DateOnly(2026, 1, 7), 80, 70, GameEstado.FINISHED);
        TestDb.Game(db, s, c, d, new DateOnly(2026, 1, 8), 80, 70, GameEstado.FINISHED);

        await StandingsCalculator.Recalcular(db, s.Id);
        var tabla = await Tabla(db, s.Id);

        var rb = tabla.Single(t => t.TeamId == b.Id);
        var rc = tabla.Single(t => t.TeamId == c.Id);

        Assert.Equal(rb.Victorias * 2 + rb.Derrotas, rc.Victorias * 2 + rc.Derrotas); // empate real
        Assert.True(rb.Posicion < rc.Posicion);      // el directo lo desempata
        Assert.Equal(1, tabla.Single(t => t.TeamId == a.Id).Posicion);
        Assert.Equal(4, tabla.Single(t => t.TeamId == d.Id).Posicion);
    }

    [Fact]
    public async Task Desempate_entre_tres_o_mas_equipos_usa_diff_entre_empatados()
    {
        var db = TestDb.Crear();
        var s = TestDb.Season(db, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        var a = Equipo(db, s, "A", "AAA");
        var b = Equipo(db, s, "B", "BBB");
        var c = Equipo(db, s, "C", "CCC");

        // Triple empate en puntos (1V1D cada uno). Diff entre empatados:
        //  A: +30 (vs B) − 1 (vs C) = +29   | B: −29   | C: 0
        TestDb.Game(db, s, a, b, new DateOnly(2026, 1, 5), 100, 70, GameEstado.FINISHED);
        TestDb.Game(db, s, b, c, new DateOnly(2026, 1, 6), 80, 79, GameEstado.FINISHED);
        TestDb.Game(db, s, c, a, new DateOnly(2026, 1, 7), 80, 79, GameEstado.FINISHED);

        await StandingsCalculator.Recalcular(db, s.Id);
        var tabla = await Tabla(db, s.Id);

        Assert.Single(tabla.Select(t => t.Victorias * 2 + t.Derrotas).Distinct()); // empatados
        Assert.Equal(3, tabla.Count);
        Assert.Equal(3, tabla.Select(t => t.Posicion).Distinct().Count());

        Assert.Equal(a.Id, tabla[0].TeamId);   // mejor diff entre empatados
        Assert.Equal(c.Id, tabla[1].TeamId);
        Assert.Equal(b.Id, tabla[2].TeamId);
    }
}
