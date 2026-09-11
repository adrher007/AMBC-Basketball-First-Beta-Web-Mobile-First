using Ambc.Api.Data;
using Ambc.Api.Models;
using Ambc.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Ambc.Api.Tests;

/// <summary>PlayoffService: series Bo3, re-seeding de SF, FINAL Bo5 y TERCER_LUGAR Bo3.</summary>
public class PlayoffTests
{
    private static PlayoffSeries Serie(AmbcDbContext db, Season s, Team a, Team b,
        FaseSerie fase, int bestOf)
    {
        var serie = new PlayoffSeries
        {
            SeasonId = s.Id,
            Fase = fase,
            TeamAId = a.Id,
            TeamBId = b.Id,
            BestOf = bestOf,
            Estado = "EN_CURSO",
        };
        db.PlayoffSeries.Add(serie);
        db.SaveChanges();
        return serie;
    }

    /// <summary>Partido FINISHED de la serie, ganado por el equipo indicado.</summary>
    private static void PartidoGanadoPor(AmbcDbContext db, Season s, PlayoffSeries serie,
        Team ganador, Team otro, DateOnly fecha)
    {
        var g = new Game
        {
            SeasonId = s.Id,
            Fase = serie.Fase.ToString(),
            PlayoffSeriesId = serie.Id,
            HomeTeamId = ganador.Id,
            AwayTeamId = otro.Id,
            Fecha = fecha,
            Hora = "20:00",
            Estado = GameEstado.FINISHED,
            MarcadorLocal = 80,
            MarcadorVisitante = 70,
            GanadorId = ganador.Id,
        };
        db.Games.Add(g);
        db.SaveChanges();
    }

    /// <summary>Deja la serie con teamA ganando `victoriasA` y teamB ganando `victoriasB`.</summary>
    private static void MontarSerie(AmbcDbContext db, Season s, PlayoffSeries serie,
        Team a, Team b, int victoriasA, int victoriasB)
    {
        var dia = 1;
        for (var i = 0; i < victoriasA; i++)
            PartidoGanadoPor(db, s, serie, a, b, new DateOnly(2026, 6, dia++));
        for (var i = 0; i < victoriasB; i++)
            PartidoGanadoPor(db, s, serie, b, a, new DateOnly(2026, 6, dia++));
    }

    [Fact]
    public async Task Bo3_se_decide_2_0()
    {
        var db = TestDb.Crear();
        var s = TestDb.Season(db, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        var a = TestDb.Team(db, "A", "AAA");
        var b = TestDb.Team(db, "B", "BBB");
        var serie = Serie(db, s, a, b, FaseSerie.QF, bestOf: 3);
        MontarSerie(db, s, serie, a, b, 2, 0);

        await PlayoffService.TrasResultado(db, s.Id, serie.Id);

        var r = await db.PlayoffSeries.FindAsync(serie.Id);
        Assert.Equal(2, r!.WinsA);
        Assert.Equal(0, r.WinsB);
        Assert.Equal(a.Id, r.GanadorId);
        Assert.Equal("FINALIZADA", r.Estado);
    }

    [Fact]
    public async Task Bo3_se_decide_2_1()
    {
        var db = TestDb.Crear();
        var s = TestDb.Season(db, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        var a = TestDb.Team(db, "A", "AAA");
        var b = TestDb.Team(db, "B", "BBB");
        var serie = Serie(db, s, a, b, FaseSerie.QF, bestOf: 3);
        MontarSerie(db, s, serie, a, b, 2, 1);

        await PlayoffService.TrasResultado(db, s.Id, serie.Id);

        var r = await db.PlayoffSeries.FindAsync(serie.Id);
        Assert.Equal(2, r!.WinsA);
        Assert.Equal(1, r.WinsB);
        Assert.Equal(a.Id, r.GanadorId);
        Assert.Equal("FINALIZADA", r.Estado);
    }

    [Fact]
    public async Task Bo3_en_1_1_la_serie_sigue_y_se_crea_el_tercer_partido()
    {
        var db = TestDb.Crear();
        var s = TestDb.Season(db, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        var a = TestDb.Team(db, "A", "AAA");
        var b = TestDb.Team(db, "B", "BBB");
        var serie = Serie(db, s, a, b, FaseSerie.QF, bestOf: 3);
        MontarSerie(db, s, serie, a, b, 1, 1);

        await PlayoffService.TrasResultado(db, s.Id, serie.Id);

        var r = await db.PlayoffSeries.FindAsync(serie.Id);
        Assert.Equal(1, r!.WinsA);
        Assert.Equal(1, r.WinsB);
        Assert.Null(r.GanadorId);
        Assert.NotEqual("FINALIZADA", r.Estado);

        var partidos = await db.Games.Where(g => g.PlayoffSeriesId == serie.Id).ToListAsync();
        Assert.Equal(3, partidos.Count);                       // se agregó el partido 3
        Assert.Contains(partidos, p => p.Estado == GameEstado.SCHEDULED);
    }

    [Fact]
    public async Task Re_seeding_de_SF_ordena_por_seed_original()
    {
        var db = TestDb.Crear();
        var s = TestDb.Season(db, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));

        // 8 equipos con posición en standings (seed 1..8)
        var equipos = new List<Team>();
        for (var i = 1; i <= 8; i++)
        {
            var t = TestDb.Team(db, $"T{i}", $"T{i}0");
            equipos.Add(t);
            db.Standings.Add(new Standing
            {
                SeasonId = s.Id, TeamId = t.Id, Posicion = i, Pj = 0,
                Victorias = 0, Derrotas = 0, Pf = 0, Pc = 0, Diff = 0,
            });
        }
        await db.SaveChangesAsync();

        // 4 series QF; el ganador de cada una es el mejor seed de la pareja:
        // 1v8 → 1 | 2v7 → 7 (sorpresa) | 3v6 → 3 | 4v5 → 4
        var parejas = new[] { (0, 7), (1, 6), (2, 5), (3, 4) };
        var ganadores = new[] { equipos[0], equipos[6], equipos[2], equipos[3] };
        var series = new List<PlayoffSeries>();

        for (var i = 0; i < 4; i++)
        {
            var (ia, ib) = parejas[i];
            var serie = Serie(db, s, equipos[ia], equipos[ib], FaseSerie.QF, bestOf: 3);
            series.Add(serie);
            // Las 3 primeras se resuelven antes; la última se resuelve en la llamada
            if (i < 3)
            {
                serie.GanadorId = ganadores[i].Id;
                serie.Estado = "FINALIZADA";
                serie.WinsA = 2;
            }
        }
        await db.SaveChangesAsync();

        // La cuarta serie se decide ahora → dispara Avanzar y crea las SF
        MontarSerie(db, s, series[3], series[3].TeamAId == equipos[3].Id ? equipos[3] : equipos[4],
            series[3].TeamAId == equipos[3].Id ? equipos[4] : equipos[3], 2, 0);
        await PlayoffService.TrasResultado(db, s.Id, series[3].Id);

        var sf = await db.PlayoffSeries
            .Where(p => p.SeasonId == s.Id && p.Fase == FaseSerie.SF)
            .OrderBy(p => p.TeamAId).ToListAsync();

        Assert.Equal(2, sf.Count);
        Assert.All(sf, x => Assert.Equal(3, x.BestOf));   // SF es Bo3

        // Re-seeding: ganadores = seeds {1, 7, 3, 4} → ordenados 1,3,4,7
        // parejas: (1 vs 7) y (3 vs 4)
        var seedsGanadores = new[] { 1, 3, 4, 7 };
        var paresEsperados = new HashSet<string>
        {
            $"{seedsGanadores[0]}-{seedsGanadores[3]}",
            $"{seedsGanadores[1]}-{seedsGanadores[2]}",
        };
        var paresReales = sf.Select(x =>
        {
            var sa = equipos.FindIndex(t => t.Id == x.TeamAId) + 1;
            var sb = equipos.FindIndex(t => t.Id == x.TeamBId) + 1;
            return $"{sa}-{sb}";
        }).ToHashSet();

        Assert.Equal(paresEsperados, paresReales);
    }

    [Fact]
    public async Task SF_resueltas_crean_FINAL_Bo5_y_TERCER_LUGAR_Bo3()
    {
        var db = TestDb.Crear();
        var s = TestDb.Season(db, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        var a = TestDb.Team(db, "A", "AAA");
        var b = TestDb.Team(db, "B", "BBB");
        var c = TestDb.Team(db, "C", "CCC");
        var d = TestDb.Team(db, "D", "DDD");

        // SF1: A vence a B (resuelta antes). SF2: se resuelve ahora (C vence a D).
        var sf1 = Serie(db, s, a, b, FaseSerie.SF, bestOf: 3);
        sf1.GanadorId = a.Id;
        sf1.Estado = "FINALIZADA";
        sf1.WinsA = 2;
        await db.SaveChangesAsync();

        var sf2 = Serie(db, s, c, d, FaseSerie.SF, bestOf: 3);
        MontarSerie(db, s, sf2, c, d, 2, 0);
        await PlayoffService.TrasResultado(db, s.Id, sf2.Id);

        var final = await db.PlayoffSeries
            .FirstOrDefaultAsync(p => p.SeasonId == s.Id && p.Fase == FaseSerie.FINAL);
        var tercer = await db.PlayoffSeries
            .FirstOrDefaultAsync(p => p.SeasonId == s.Id && p.Fase == FaseSerie.TERCER_LUGAR);

        Assert.NotNull(final);
        Assert.Equal(5, final!.BestOf);                       // FINAL Bo5
        Assert.NotNull(tercer);
        Assert.Equal(3, tercer!.BestOf);                      // TERCER_LUGAR Bo3

        // FINAL: ganadores (A y C). TERCER_LUGAR: perdedores (B y D).
        var idsFinal = new[] { final.TeamAId, final.TeamBId }.ToHashSet();
        Assert.Equal(new HashSet<Guid> { a.Id, c.Id }, idsFinal);

        var idsTercer = new[] { tercer.TeamAId, tercer.TeamBId }.ToHashSet();
        Assert.Equal(new HashSet<Guid> { b.Id, d.Id }, idsTercer);

        // Cada serie nueva arranca con su primer partido programado
        Assert.Equal(1, await db.Games.CountAsync(g => g.PlayoffSeriesId == final.Id));
        Assert.Equal(1, await db.Games.CountAsync(g => g.PlayoffSeriesId == tercer.Id));
    }
}
