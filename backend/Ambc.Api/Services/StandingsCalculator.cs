using Ambc.Api.Models;
using Ambc.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Ambc.Api.Services;

/// <summary>
/// Calcula la clasificación (standings) de una temporada.
/// Puntos: victoria=2, derrota=1. Sin empates (prórroga).
/// Desempates: (2 equipos) enfrentamiento directo; (3+) puntos entre empatados,
/// diff entre empatados, diff total, PF total, sorteo; recursión en subgrupos.
/// </summary>
public static class StandingsCalculator
{
    public static async Task Recalcular(AmbcDbContext db, Guid seasonId)
    {
        var games = await db.Games
            .Where(g => g.SeasonId == seasonId && g.Estado == GameEstado.FINISHED)
            .ToListAsync();

        var teams = await db.Teams
            .Where(t => db.Rosters.Any(r => r.SeasonId == seasonId && r.TeamId == t.Id))
            .Select(t => t.Id)
            .ToListAsync();

        // Acumulan partido contra partido.
        // Opción A: si un partido FINISHED involucra un equipo que no está en el
        // diccionario (p. ej. sin roster en la temporada), se inicializa con 0 stats
        // en lugar de lanzar KeyNotFoundException y romper todo el cálculo.
        var stats = teams.ToDictionary(t => t, _ => new Acc());

        foreach (var g in games)
        {
            if (!stats.ContainsKey(g.HomeTeamId)) stats[g.HomeTeamId] = new Acc();
            if (!stats.ContainsKey(g.AwayTeamId)) stats[g.AwayTeamId] = new Acc();
        }

        foreach (var g in games)
        {
            var home = stats[g.HomeTeamId];
            var away = stats[g.AwayTeamId];

            home.Pj++; away.Pj++;
            home.Pf += g.MarcadorLocal; home.Pc += g.MarcadorVisitante;
            away.Pf += g.MarcadorVisitante; away.Pc += g.MarcadorLocal;

            if (g.MarcadorLocal > g.MarcadorVisitante) { home.V++; away.D++; }
            else { away.V++; home.D++; }
        }

        int Pts(Guid id) => stats[id].V * 2 + stats[id].D;

        var sorted = SortTeams(teams, games, stats, Pts);

        // Reemplaza la tabla standings en UNA sola transacción: si algo falla
        // no puede quedar una tabla a medias (borrada sin repoblar).
        // Si ya hay una transacción ambiente (p. ej. Confirmar de planilla),
        // se participa de ella y el commit lo hace el llamador.
        var txExistente = db.Database.CurrentTransaction;
        var tx = txExistente is null ? await db.Database.BeginTransactionAsync() : null;

        try
        {
            var old = await db.Standings.Where(s => s.SeasonId == seasonId).ToListAsync();
            db.Standings.RemoveRange(old);
            await db.SaveChangesAsync();

            for (var i = 0; i < sorted.Count; i++)
            {
                var id = sorted[i];
                var a = stats[id];
                db.Standings.Add(new Standing
                {
                    SeasonId = seasonId,
                    TeamId = id,
                    Pj = a.Pj,
                    Victorias = a.V,
                    Derrotas = a.D,
                    Pf = a.Pf,
                    Pc = a.Pc,
                    Diff = a.Pf - a.Pc,
                    Posicion = i + 1,
                });
            }
            await db.SaveChangesAsync();

            if (tx is not null) await tx.CommitAsync();
        }
        catch
        {
            if (tx is not null) await tx.RollbackAsync();
            throw;
        }
        finally
        {
            if (tx is not null) await tx.DisposeAsync();
        }
    }

    private sealed class Acc
    {
        public int Pj, V, D, Pf, Pc;
    }

    // ===== Ordenamiento con desempates =====

    private static List<Guid> SortTeams(
        List<Guid> teams,
        List<Game> games,
        Dictionary<Guid, Acc> stats,
        Func<Guid, int> pts)
    {
        // Agrupar por puntos desc
        var groups = teams
            .GroupBy(pts)
            .OrderByDescending(g => g.Key)
            .Select(g => g.ToList())
            .ToList();

        var result = new List<Guid>();
        foreach (var group in groups)
        {
            result.AddRange(ResolveGroup(group, games, stats, pts));
        }
        return result;
    }

    private static List<Guid> ResolveGroup(
        List<Guid> group,
        List<Game> games,
        Dictionary<Guid, Acc> stats,
        Func<Guid, int> pts)
    {
        if (group.Count <= 1) return group;

        // 2 equipos: enfrentamiento directo
        if (group.Count == 2)
            return ResolveTwo(group[0], group[1], games, stats);

        // 3+: criterios en cascada
        return ResolveMany(group, games, stats, stage: 0);
    }

    // Orden de criterios para 3+:
    //  0 = puntos entre empatados
    //  1 = diff entre empatados
    //  2 = diff total
    //  3 = PF total
    //  4 = sorteo (por id)
    private static List<Guid> ResolveMany(
        List<Guid> group,
        List<Game> games,
        Dictionary<Guid, Acc> stats,
        int stage)
    {
        if (group.Count <= 1) return group;
        if (group.Count == 2) return ResolveTwo(group[0], group[1], games, stats);
        if (stage >= 4) return group.OrderBy(t => t).ToList(); // sorteo

        // Calcular métrica del grupo para esta etapa
        var metric = group.ToDictionary(t => t, t => Metric(t, group, games, stats, stage));

        var subGroups = group
            .GroupBy(t => metric[t])
            .OrderByDescending(g => g.Key)
            .Select(g => g.ToList())
            .ToList();

        var result = new List<Guid>();
        foreach (var sub in subGroups)
        {
            if (sub.Count == 1)
                result.Add(sub[0]);
            else
                result.AddRange(ResolveMany(sub, games, stats, stage + 1));
        }
        return result;
    }

    private static int Metric(
        Guid team,
        List<Guid> group,
        List<Game> games,
        Dictionary<Guid, Acc> stats,
        int stage)
    {
        var set = group.ToHashSet();
        var acc = stats[team];

        switch (stage)
        {
            case 0: // puntos entre empatados (solo partidos dentro del grupo)
            {
                int pts = 0;
                foreach (var g in games.Where(g => set.Contains(g.HomeTeamId) && set.Contains(g.AwayTeamId)))
                {
                    if (g.HomeTeamId == team) pts += g.MarcadorLocal > g.MarcadorVisitante ? 2 : 1;
                    else if (g.AwayTeamId == team) pts += g.MarcadorVisitante > g.MarcadorLocal ? 2 : 1;
                }
                return pts;
            }
            case 1: // diff entre empatados
            {
                int diff = 0;
                foreach (var g in games.Where(g => set.Contains(g.HomeTeamId) && set.Contains(g.AwayTeamId)))
                {
                    if (g.HomeTeamId == team) diff += g.MarcadorLocal - g.MarcadorVisitante;
                    else if (g.AwayTeamId == team) diff += g.MarcadorVisitante - g.MarcadorLocal;
                }
                return diff;
            }
            case 2: // diff total
                return acc.Pf - acc.Pc;
            case 3: // PF total
                return acc.Pf;
            default: // sorteo
                return 0;
        }
    }

    private static List<Guid> ResolveTwo(
        Guid a, Guid b,
        List<Game> games,
        Dictionary<Guid, Acc> stats)
    {
        int winsA = 0, winsB = 0, diffA = 0;
        foreach (var g in games)
        {
            bool duel = (g.HomeTeamId == a && g.AwayTeamId == b) ||
                        (g.HomeTeamId == b && g.AwayTeamId == a);
            if (!duel) continue;

            int pa = g.HomeTeamId == a ? g.MarcadorLocal : g.MarcadorVisitante;
            int pb = g.HomeTeamId == a ? g.MarcadorVisitante : g.MarcadorLocal;
            if (pa > pb) winsA++; else winsB++;
            diffA += pa - pb;
        }

        if (winsA != winsB) return winsA > winsB ? [a, b] : [b, a];
        if (diffA != 0) return diffA > 0 ? [a, b] : [b, a];

        int dtA = stats[a].Pf - stats[a].Pc;
        int dtB = stats[b].Pf - stats[b].Pc;
        if (dtA != dtB) return dtA > dtB ? [a, b] : [b, a];

        if (stats[a].Pf != stats[b].Pf)
            return stats[a].Pf > stats[b].Pf ? [a, b] : [b, a];

        return a.CompareTo(b) <= 0 ? [a, b] : [b, a];
    }
}
