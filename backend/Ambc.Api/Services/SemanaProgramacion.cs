using Microsoft.EntityFrameworkCore;
using Ambc.Api.Data;
using Ambc.Api.Models;

namespace Ambc.Api.Services;

/// <summary>
/// Programación SEMANAL: la semana es la unidad administrativa de planificación,
/// el Game sigue siendo la unidad deportiva.
///
/// Reutiliza el algoritmo round-robin existente (FixtureGenerator) pero crea
/// únicamente los enfrentamientos correspondientes a la semana elegida:
///  - descarta las rondas cuyos enfrentamientos ya existen como Games
///  - 1 ronda por día, máximo 4 partidos por jornada (9 equipos → 1 libre)
///  - horarios del sistema original (19:00 - 22:00)
///  - permite ida y vuelta (los mismos equipos pueden reencontrarse en otra semana)
/// </summary>
public static class SemanaProgramacion
{
    public const int PartidosPorJornada = 4;
    public static readonly string[] Horas = { "19:00", "20:00", "21:00", "22:00" };

    /// <summary>Número de semana (1..N) de una fecha respecto al inicio de la temporada.</summary>
    public static int SemanaDe(Season season, DateOnly fecha)
    {
        var dias = fecha.DayNumber - season.FechaInicio.DayNumber;
        if (dias < 0) return 1;
        return (dias / 7) + 1;
    }

    /// <summary>Primer día de la semana N.</summary>
    public static DateOnly InicioDeSemana(Season season, int semana) =>
        season.FechaInicio.AddDays((semana - 1) * 7);

    /// <summary>Rondas del fixture (ida + vuelta) que todavía no tienen Games creados.</summary>
    public static async Task<List<List<(Guid Home, Guid Away)>>> RondasPendientes(
        AmbcDbContext db, Guid seasonId, List<Guid> equipos)
    {
        var ida = FixtureGenerator.GenerateRoundRobin(equipos, isReturnLeg: false);
        var vuelta = FixtureGenerator.GenerateRoundRobin(equipos, isReturnLeg: true);
        var todas = ida.Concat(vuelta).ToList();

        var existentes = await db.Games
            .Where(g => g.SeasonId == seasonId)
            .Select(g => new { g.HomeTeamId, g.AwayTeamId })
            .ToListAsync();

        var set = existentes.Select(e => (e.HomeTeamId, e.AwayTeamId)).ToHashSet();

        // Una ronda se descarta si CUALQUIERA de sus enfrentamientos ya existe
        return todas
            .Where(ronda => ronda.All(p => !set.Contains((p.Home, p.Away))))
            .ToList();
    }

    /// <summary>
    /// Propuesta automática: primeras rondas pendientes repartidas en los días
    /// de la semana indicada (1 ronda por día), respetando fechas ya ocupadas.
    /// </summary>
    public static async Task<(string? error, List<PropuestaPartido> propuesta)> Proponer(
        AmbcDbContext db, Guid seasonId, int semana)
    {
        var season = await db.Seasons.FindAsync(seasonId);
        if (season is null) return ("Temporada no encontrada.", new());

        var equipos = await db.Teams
            .Where(t => db.Rosters.Any(r => r.SeasonId == seasonId && r.TeamId == t.Id))
            .OrderBy(t => t.Nombre)
            .Select(t => t.Id)
            .ToListAsync();

        if (equipos.Count != 9)
            return ("Se requieren exactamente 9 equipos con plantilla para programar la semana.", new());

        var pendientes = await RondasPendientes(db, seasonId, equipos);
        if (pendientes.Count == 0)
            return ("No quedan enfrentamientos pendientes en esta temporada.", new());

        // Días de la semana ocupados por partidos ya programados
        var inicioSemana = InicioDeSemana(season, semana);
        var finSemana = inicioSemana.AddDays(6);
        var ocupados = await db.Games
            .Where(g => g.SeasonId == seasonId && g.Fecha >= inicioSemana && g.Fecha <= finSemana)
            .Select(g => g.Fecha)
            .Distinct()
            .ToListAsync();

        var propuesta = new List<PropuestaPartido>();
        var idx = 0;

        for (var dia = 0; dia < 7 && idx < pendientes.Count; dia++)
        {
            var fecha = inicioSemana.AddDays(dia);
            if (fecha > season.FechaFin) break;
            if (ocupados.Contains(fecha)) continue;   // ese día ya tiene jornada

            var ronda = pendientes[idx];
            if (ronda.Count > PartidosPorJornada) break;

            for (var k = 0; k < ronda.Count; k++)
            {
                propuesta.Add(new PropuestaPartido
                {
                    Fecha = fecha,
                    Hora = Horas[k],
                    HomeTeamId = ronda[k].Home,
                    AwayTeamId = ronda[k].Away,
                });
            }
            idx++;
        }

        return (null, propuesta);
    }

    /// <summary>
    /// Valida los partidos de una semana antes de crear los Games.
    /// Reglas: local ≠ visitante, sin duplicados dentro de la semana,
    /// un equipo no juega dos veces el mismo día, máximo 4 por jornada, 2..3 árbitros.
    /// </summary>
    public static async Task<string?> ValidarSemana(
        AmbcDbContext db, Guid seasonId, int semana, List<SemanaPartidoDto> partidos)
    {
        if (partidos.Count == 0) return "La semana no tiene partidos.";

        var season = await db.Seasons.FindAsync(seasonId);
        if (season is null) return "Temporada no encontrada.";

        var inicioSemana = InicioDeSemana(season, semana);
        var finSemana = inicioSemana.AddDays(6);

        foreach (var p in partidos)
        {
            if (p.HomeTeamId == p.AwayTeamId)
                return "El equipo local y el visitante deben ser diferentes.";
            if (string.IsNullOrWhiteSpace(p.Hora))
                return "Cada partido necesita una hora.";
            if (p.Fecha < inicioSemana || p.Fecha > finSemana)
                return $"La fecha {p.Fecha:dd/MM/yyyy} no pertenece a la semana {semana}.";
            if (p.ArbitroIds is null || p.ArbitroIds.Count < 2)
                return "Cada partido requiere al menos 2 árbitros.";
            if (p.ArbitroIds.Count > 3)
                return "Cada partido admite como máximo 3 árbitros.";
            if (p.ArbitroIds.Distinct().Count() != p.ArbitroIds.Count)
                return "No puedes asignar el mismo árbitro dos veces al mismo partido.";
        }

        // Máximo por jornada (día)
        foreach (var grupo in partidos.GroupBy(p => p.Fecha))
        {
            if (grupo.Count() > PartidosPorJornada)
                return $"La jornada {grupo.Key:dd/MM/yyyy} supera el máximo de {PartidosPorJornada} partidos.";
        }

        // Duplicados dentro de la semana (misma pareja repetida)
        var parejas = partidos.Select(p => (p.HomeTeamId, p.AwayTeamId)).ToList();
        if (parejas.Distinct().Count() != parejas.Count)
            return "Hay enfrentamientos duplicados dentro de la misma semana.";

        // No repetir un enfrentamiento ya programado en la temporada (ida/vuelta permite
        // la pareja invertida, no la misma nuevamente)
        var idsEquiposSemana = partidos.SelectMany(p => new[] { p.HomeTeamId, p.AwayTeamId }).Distinct().ToList();
        var yaProgramados = await db.Games
            .Where(g => g.SeasonId == seasonId
                && idsEquiposSemana.Contains(g.HomeTeamId)
                && idsEquiposSemana.Contains(g.AwayTeamId))
            .Select(g => new { g.HomeTeamId, g.AwayTeamId })
            .ToListAsync();

        var setProgramados = yaProgramados.Select(x => (x.HomeTeamId, x.AwayTeamId)).ToHashSet();
        var repetido = parejas.FirstOrDefault(p => setProgramados.Contains(p));
        if (repetido != default)
            return "Uno de los enfrentamientos ya está programado en esta temporada (misma localía).";

        // Un equipo no puede jugar dos veces el mismo día
        foreach (var grupo in partidos.GroupBy(p => p.Fecha))
        {
            var equiposDia = new List<Guid>();
            foreach (var p in grupo)
            {
                equiposDia.Add(p.HomeTeamId);
                equiposDia.Add(p.AwayTeamId);
            }
            if (equiposDia.Distinct().Count() != equiposDia.Count)
                return $"Un equipo no puede jugar dos veces el mismo día ({grupo.Key:dd/MM/yyyy}).";
        }

        // Equipos existentes
        var idsEquipos = partidos.SelectMany(p => new[] { p.HomeTeamId, p.AwayTeamId }).Distinct().ToList();
        var equiposOk = await db.Teams.CountAsync(t => idsEquipos.Contains(t.Id));
        if (equiposOk != idsEquipos.Count) return "Uno o más equipos no existen.";

        // Árbitros: existen, activos y con rol ARBITRO
        var idsArbitros = partidos.SelectMany(p => p.ArbitroIds!).Distinct().ToList();
        var arbitrosOk = await db.Referees
            .Where(r => idsArbitros.Contains(r.Id))
            .Join(db.Users, r => r.UserId, u => u.Id, (r, u) => new { r.Id, u.Rol, u.Activo })
            .ToListAsync();

        if (arbitrosOk.Count != idsArbitros.Count)
            return "Uno o más árbitros seleccionados no existen.";
        if (arbitrosOk.Any(a => a.Rol != Rol.ARBITRO || !a.Activo))
            return "Solo se pueden asignar usuarios activos con rol Árbitro.";

        return null;
    }
}

public class PropuestaPartido
{
    public DateOnly Fecha { get; set; }
    public string Hora { get; set; } = "";
    public Guid HomeTeamId { get; set; }
    public Guid AwayTeamId { get; set; }
}

public class SemanaPartidoDto
{
    public DateOnly Fecha { get; set; }
    public string Hora { get; set; } = "";
    public Guid HomeTeamId { get; set; }
    public Guid AwayTeamId { get; set; }
    public List<Guid>? ArbitroIds { get; set; }
}

public class ConfirmarSemanaDto
{
    public Guid SeasonId { get; set; }
    public int Semana { get; set; }
    public List<SemanaPartidoDto> Partidos { get; set; } = new();
}
