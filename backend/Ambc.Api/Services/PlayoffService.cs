using Ambc.Api.Models;
using Ambc.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Ambc.Api.Services;

/// <summary>
/// Lógica de playoffs: creación de series y partidos, avance de fases.
/// QF Bo3 (1v8,2v7,3v6,4v5) → SF Bo3 (re-seeding) → FINAL Bo5, TERCER_LUGAR Bo3.
/// Se crea un partido por vez; al resolverse, se crea el siguiente solo si la serie sigue abierta.
/// </summary>
public static class PlayoffService
{
    // Fases en orden
    private static readonly FaseSerie[] Orden = { FaseSerie.QF, FaseSerie.SF, FaseSerie.FINAL, FaseSerie.TERCER_LUGAR };

    public static async Task<(bool ok, string error)> Iniciar(AmbcDbContext db, Guid seasonId)
    {
        // Validar que no haya series ya
        if (await db.PlayoffSeries.AnyAsync(p => p.SeasonId == seasonId))
            return (false, "Los playoffs ya fueron iniciados.");

        // Validar fase regular completa (72 partidos FINISHED, sin los de playoff)
        var regularGames = await db.Games
            .Where(g => g.SeasonId == seasonId && g.PlayoffSeriesId == null)
            .ToListAsync();

        var totalRegular = regularGames.Count;
        var finishedRegular = regularGames.Count(g => g.Estado == GameEstado.FINISHED);
        if (totalRegular != 72 || finishedRegular != 72)
            return (false, $"La fase regular no está completa ({finishedRegular}/{totalRegular} partidos finalizados).");

        // Tomar el top 8 del standings
        var standings = await db.Standings
            .Where(s => s.SeasonId == seasonId)
            .OrderBy(s => s.Posicion)
            .ToListAsync();

        if (standings.Count < 8)
            return (false, "Se requieren 8 equipos clasificados.");

        // QF: 1v8, 2v7, 3v6, 4v5
        var seeds = standings.Select(s => s.TeamId).ToList();
        var qfPairs = new[]
        {
            (seeds[0], seeds[7]),
            (seeds[1], seeds[6]),
            (seeds[2], seeds[5]),
            (seeds[3], seeds[4]),
        };

        foreach (var (a, b) in qfPairs)
        {
            var serie = new PlayoffSeries
            {
                SeasonId = seasonId,
                Fase = FaseSerie.QF,
                TeamAId = a,
                TeamBId = b,
                BestOf = 3,
                Estado = "EN_CURSO",
            };
            db.PlayoffSeries.Add(serie);
            await db.SaveChangesAsync(); // obtener serie.Id

            db.Games.Add(CrearPartido(db, seasonId, serie, 1, null));
        }
        await db.SaveChangesAsync();
        return (true, "Playoffs iniciados: 4 series de QF creadas.");
    }

    private static Game CrearPartido(AmbcDbContext db, Guid seasonId, PlayoffSeries serie, int numero, DateOnly? fecha)
    {
        // Alternar localía: partido 1 y 3 lo juega TeamA en casa; 2 y 4 TeamB
        bool aHome = numero % 2 == 1;
        return new Game
        {
            SeasonId = seasonId,
            Fase = serie.Fase.ToString(),
            PlayoffSeriesId = serie.Id,
            HomeTeamId = aHome ? serie.TeamAId : serie.TeamBId,
            AwayTeamId = aHome ? serie.TeamBId : serie.TeamAId,
            Fecha = fecha ?? DateOnly.FromDateTime(DateTime.UtcNow),
            Hora = "20:00",
            Estado = GameEstado.SCHEDULED,
        };
    }

    /// <summary>
    /// Se llama tras registrar el resultado de un partido de playoff.
    /// Resuelve la serie (marca ganador si corresponde), avanza fases si toca,
    /// y crea el siguiente partido de la serie si sigue abierta.
    /// </summary>
    public static async Task TrasResultado(AmbcDbContext db, Guid seasonId, Guid serieId)
    {
        var serie = await db.PlayoffSeries.FindAsync(serieId);
        if (serie is null || serie.GanadorId is not null) return;

        // Recalcular wins a partir de los partidos terminados de la serie
        var partidos = await db.Games
            .Where(g => g.PlayoffSeriesId == serieId && g.Estado == GameEstado.FINISHED)
            .ToListAsync();

        int winsA = 0, winsB = 0;
        foreach (var p in partidos)
        {
            if (p.GanadorId == serie.TeamAId) winsA++;
            else if (p.GanadorId == serie.TeamBId) winsB++;
        }

        var needed = (serie.BestOf / 2) + 1; // Bo3 → 2, Bo5 → 3

        // ¿Serie decidida?
        if (winsA >= needed || winsB >= needed)
        {
            serie.WinsA = winsA;
            serie.WinsB = winsB;
            serie.GanadorId = winsA >= needed ? serie.TeamAId : serie.TeamBId;
            serie.Estado = "FINALIZADA";
            await db.SaveChangesAsync();

            // Avanzar la siguiente fase si todas las series de esta fase están resueltas
            await Avanzar(db, seasonId);
            return;
        }

        // Serie sigue abierta: actualizar wins y crear el siguiente partido
        serie.WinsA = winsA;
        serie.WinsB = winsB;
        await db.SaveChangesAsync();

        var siguienteNumero = partidos.Count + 1;
        var maxPartidos = serie.BestOf;
        if (siguienteNumero <= maxPartidos)
        {
            db.Games.Add(CrearPartido(db, seasonId, serie, siguienteNumero, null));
            await db.SaveChangesAsync();
        }
    }

    private static async Task Avanzar(AmbcDbContext db, Guid seasonId)
    {
        // QF → SF
        var qf = await db.PlayoffSeries.Where(p => p.SeasonId == seasonId && p.Fase == FaseSerie.QF).ToListAsync();
        if (qf.Count == 4 && qf.All(s => s.GanadorId is not null))
        {
            bool sfExiste = await db.PlayoffSeries.AnyAsync(p => p.SeasonId == seasonId && p.Fase == FaseSerie.SF);
            if (!sfExiste)
            {
                // Re-seeding: ordenar los 4 ganadores por su seed original (posición en standings)
                var seeds = await db.Standings
                    .Where(s => s.SeasonId == seasonId)
                    .OrderBy(s => s.Posicion)
                    .ToDictionaryAsync(s => s.TeamId, s => s.Posicion);

                var ganadores = qf.Select(s => s.GanadorId!.Value).ToList();
                var ordenados = ganadores.OrderBy(g => seeds.GetValueOrDefault(g, 99)).ToList();

                var sfPairs = new[]
                {
                    (ordenados[0], ordenados[3]), // mejor vs 4º
                    (ordenados[1], ordenados[2]), // 2º vs 3º
                };
                foreach (var (a, b) in sfPairs)
                {
                    var serie = new PlayoffSeries
                    {
                        SeasonId = seasonId,
                        Fase = FaseSerie.SF,
                        TeamAId = a,
                        TeamBId = b,
                        BestOf = 3,
                        Estado = "EN_CURSO",
                    };
                    db.PlayoffSeries.Add(serie);
                    await db.SaveChangesAsync();
                    db.Games.Add(CrearPartido(db, seasonId, serie, 1, null));
                }
                await db.SaveChangesAsync();
            }
        }

        // SF → FINAL + TERCER_LUGAR
        var sf = await db.PlayoffSeries.Where(p => p.SeasonId == seasonId && p.Fase == FaseSerie.SF).ToListAsync();
        if (sf.Count == 2 && sf.All(s => s.GanadorId is not null))
        {
            bool finalExiste = await db.PlayoffSeries.AnyAsync(p => p.SeasonId == seasonId && p.Fase == FaseSerie.FINAL);
            if (!finalExiste)
            {
                // Ganadores SF → FINAL (Bo5)
                var finalistas = sf.Select(s => s.GanadorId!.Value).ToList();
                var final = new PlayoffSeries
                {
                    SeasonId = seasonId,
                    Fase = FaseSerie.FINAL,
                    TeamAId = finalistas[0],
                    TeamBId = finalistas[1],
                    BestOf = 5,
                    Estado = "EN_CURSO",
                };
                db.PlayoffSeries.Add(final);
                await db.SaveChangesAsync();
                db.Games.Add(CrearPartido(db, seasonId, final, 1, null));

                // Perdedores SF → TERCER_LUGAR (Bo3)
                var perdedores = sf.Select(s =>
                    s.GanadorId == s.TeamAId ? s.TeamBId : s.TeamAId).ToList();
                var tercer = new PlayoffSeries
                {
                    SeasonId = seasonId,
                    Fase = FaseSerie.TERCER_LUGAR,
                    TeamAId = perdedores[0],
                    TeamBId = perdedores[1],
                    BestOf = 3,
                    Estado = "EN_CURSO",
                };
                db.PlayoffSeries.Add(tercer);
                await db.SaveChangesAsync();
                db.Games.Add(CrearPartido(db, seasonId, tercer, 1, null));

                await db.SaveChangesAsync();
            }
        }
    }
}
