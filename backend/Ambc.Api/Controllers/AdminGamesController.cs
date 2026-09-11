using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ambc.Api.Models;
using Ambc.Api.Data;
using Ambc.Api.Services;

namespace Ambc.Api.Controllers;

[ApiController]
[Route("api/admin/games")]
[Authorize(Roles = "ADMIN_AMBC")]
public class AdminGamesController : ControllerBase
{
    private readonly AmbcDbContext _db;
    public AdminGamesController(AmbcDbContext db) => _db = db;

    private const int MinArbitros = 2;
    private const int MaxArbitros = 3;

    // ===== Listar partidos (equipos + árbitros asignados) =====
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid? seasonId)
    {
        var q = _db.Games.AsQueryable();
        if (seasonId is not null) q = q.Where(g => g.SeasonId == seasonId.Value);

        var games = await q
            .OrderBy(g => g.Fecha).ThenBy(g => g.Hora)
            .Select(g => new
            {
                g.Id,
                g.SeasonId,
                g.Fase,
                g.Fecha,
                g.Hora,
                g.Estado,
                g.Semana,
                g.HomeTeamId,
                g.AwayTeamId,
                g.MarcadorLocal,
                g.MarcadorVisitante,
            })
            .ToListAsync();

        var teams = await _db.Teams.ToDictionaryAsync(t => t.Id, t => t.NombreCorto);

        // Árbitros por partido
        var asignaciones = await _db.GameReferees
            .Join(_db.Referees, gr => gr.RefereeId, r => r.Id, (gr, r) => new { gr.GameId, r.Id, r.UserId })
            .Join(_db.Users, x => x.UserId, u => u.Id, (x, u) => new { x.GameId, RefereeId = x.Id, u.Nombre })
            .ToListAsync();

        var porPartido = asignaciones
            .GroupBy(a => a.GameId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(a => new ArbitroAsignadoDto { Id = a.RefereeId, Nombre = a.Nombre }).ToList());

        // ¿Tiene planilla?
        var planillas = await _db.Planillas
            .Select(p => new { p.GameId, Estado = p.Estado.ToString() })
            .ToListAsync();
        var planillaPorPartido = planillas.GroupBy(p => p.GameId)
            .ToDictionary(g => g.Key, g => g.First().Estado);

        var result = games.Select(g => new
        {
            g.Id,
            g.SeasonId,
            g.Fase,
            g.Fecha,
            g.Hora,
            Estado = g.Estado.ToString(),
            g.Semana,
            Local = teams.GetValueOrDefault(g.HomeTeamId, "?"),
            Visitante = teams.GetValueOrDefault(g.AwayTeamId, "?"),
            HomeTeamId = g.HomeTeamId,
            AwayTeamId = g.AwayTeamId,
            MarcadorLocal = g.Estado == GameEstado.FINISHED ? g.MarcadorLocal : (int?)null,
            MarcadorVisitante = g.Estado == GameEstado.FINISHED ? g.MarcadorVisitante : (int?)null,
            Arbitros = porPartido.GetValueOrDefault(g.Id, new List<ArbitroAsignadoDto>()),
            PlanillaEstado = planillaPorPartido.GetValueOrDefault(g.Id),
        });

        return Ok(result);
    }

    // ===== Crear partido y asignar árbitros (2..3) =====
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateGameDto dto)
    {
        if (dto.HomeTeamId == dto.AwayTeamId)
            return BadRequest(new { error = "El equipo local y el visitante deben ser diferentes." });

        var season = await _db.Seasons.FindAsync(dto.SeasonId);
        if (season is null) return NotFound(new { error = "Temporada no encontrada." });

        var val = await ValidarArbitros(dto.ArbitroIds);
        if (val.error is not null) return BadRequest(new { error = val.error });

        if (!await _db.Teams.AnyAsync(t => t.Id == dto.HomeTeamId)
            || !await _db.Teams.AnyAsync(t => t.Id == dto.AwayTeamId))
            return NotFound(new { error = "Equipo no encontrado." });

        var adminId = GetUserId();

        var game = new Game
        {
            SeasonId = dto.SeasonId,
            Fase = string.IsNullOrWhiteSpace(dto.Fase) ? "REGULAR" : dto.Fase.Trim(),
            HomeTeamId = dto.HomeTeamId,
            AwayTeamId = dto.AwayTeamId,
            Fecha = dto.Fecha,
            Hora = dto.Hora,
            Estado = GameEstado.SCHEDULED,
            Semana = SemanaProgramacion.SemanaDe(season, dto.Fecha),
        };
        _db.Games.Add(game);
        await _db.SaveChangesAsync();

        foreach (var refereeId in val.ids)
        {
            _db.GameReferees.Add(new GameReferee
            {
                GameId = game.Id,
                RefereeId = refereeId,
                AssignedAt = DateTime.UtcNow,
                AssignedBy = adminId ?? Guid.Empty,
            });
        }
        await _db.SaveChangesAsync();

        await NotificarArbitros(game, val.ids, adminId);

        return Ok(new { id = game.Id, arbitros = val.ids.Count });
    }

    // ===== Modificar árbitros de un partido (siempre 2..3) =====
    [HttpPut("{id}/arbitros")]
    public async Task<IActionResult> SetArbitros(Guid id, [FromBody] SetArbitrosDto dto)
    {
        var game = await _db.Games.FindAsync(id);
        if (game is null) return NotFound(new { error = "Partido no encontrado." });

        var val = await ValidarArbitros(dto.Efectivos);
        if (val.error is not null) return BadRequest(new { error = val.error });

        var adminId = GetUserId();
        var actuales = await _db.GameReferees.Where(gr => gr.GameId == id).ToListAsync();
        var ids = val.ids;

        // Quitar los que ya no están
        var quitar = actuales.Where(a => !ids.Contains(a.RefereeId)).ToList();
        _db.GameReferees.RemoveRange(quitar);

        // Agregar los nuevos
        var yaEstan = actuales.Select(a => a.RefereeId).ToHashSet();
        var nuevos = ids.Where(r => !yaEstan.Contains(r)).ToList();
        foreach (var refereeId in nuevos)
        {
            _db.GameReferees.Add(new GameReferee
            {
                GameId = id,
                RefereeId = refereeId,
                AssignedAt = DateTime.UtcNow,
                AssignedBy = adminId ?? Guid.Empty,
            });
        }
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            // 1.3 — Otro admin modificó las asignaciones entretanto
            return Conflict(new { error = "Los árbitros del partido fueron modificados por otra sesión. Recarga e intenta de nuevo." });
        }

        if (nuevos.Count > 0)
            await NotificarArbitros(game, nuevos, adminId);

        return Ok(new { id, arbitros = ids.Count });
    }

    // ===== Fase 7: Reprogramar partido (solo SCHEDULED) =====
    [HttpPut("{id}/reschedule")]
    public async Task<IActionResult> Reschedule(Guid id, [FromBody] RescheduleDto dto)
    {
        var game = await _db.Games.FindAsync(id);
        if (game is null) return NotFound(new { error = "Partido no encontrado." });
        if (game.Estado != GameEstado.SCHEDULED)
            return BadRequest(new { error = "Solo se pueden reprogramar partidos pendientes." });

        game.Fecha = dto.Fecha;
        game.Hora = dto.Hora;
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            // 1.3 — Otro admin modificó el partido entretanto
            return Conflict(new { error = "El partido fue modificado por otra sesión. Recarga e intenta de nuevo." });
        }
        return NoContent();
    }

    // ===== Fase 8: Cargar resultado final (deduce ganador, marca FINISHED) =====
    [HttpPut("{id}/result")]
    public async Task<IActionResult> SetResult(Guid id, [FromBody] ResultDto dto)
    {
        if (dto.MarcadorLocal < 0 || dto.MarcadorVisitante < 0)
            return BadRequest(new { error = "Los marcadores no pueden ser negativos." });

        var game = await _db.Games.FindAsync(id);
        if (game is null) return NotFound(new { error = "Partido no encontrado." });
        if (game.Estado == GameEstado.FINISHED)
            return BadRequest(new { error = "El partido ya tiene resultado." });

        if (dto.MarcadorLocal == dto.MarcadorVisitante)
            return BadRequest(new { error = "No se permiten empates. Carga un ganador (prórroga)." });

        game.MarcadorLocal = dto.MarcadorLocal;
        game.MarcadorVisitante = dto.MarcadorVisitante;
        game.GanadorId = dto.MarcadorLocal > dto.MarcadorVisitante
            ? game.HomeTeamId
            : game.AwayTeamId;
        game.Estado = GameEstado.FINISHED;

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            // 1.3 — Otro admin/árbitro modificó el partido entretanto
            return Conflict(new { error = "El partido fue modificado por otra sesión. Recarga e intenta de nuevo." });
        }

        // Fase 9: recalcular standings automáticamente (solo fase regular)
        if (game.PlayoffSeriesId is null)
            await StandingsCalculator.Recalcular(_db, game.SeasonId);

        // Fase 10: si es partido de playoff, resolver la serie
        if (game.PlayoffSeriesId is not null)
            await PlayoffService.TrasResultado(_db, game.SeasonId, game.PlayoffSeriesId.Value);

        return Ok(new
        {
            game.MarcadorLocal,
            game.MarcadorVisitante,
            game.GanadorId,
            Estado = game.Estado.ToString(),
        });
    }

    // ===== Helpers =====

    private async Task<(string? error, List<Guid> ids)> ValidarArbitros(List<Guid>? arbitroIds)
    {
        var raw = arbitroIds ?? new List<Guid>();
        var ids = raw.Distinct().ToList();

        // Mismo árbitro repetido: rechazar explícitamente (UNIQUE(game_id, referee_id))
        if (ids.Count != raw.Count)
            return ("No puedes asignar el mismo árbitro dos veces al mismo partido.", ids);

        if (ids.Count < MinArbitros)
            return ($"Se requieren al menos {MinArbitros} árbitros para el partido.", ids);
        if (ids.Count > MaxArbitros)
            return ($"Se permiten como máximo {MaxArbitros} árbitros por partido.", ids);

        var existentes = await _db.Referees
            .Where(r => ids.Contains(r.Id))
            .Join(_db.Users, r => r.UserId, u => u.Id, (r, u) => new { r.Id, u.Rol, u.Activo })
            .ToListAsync();

        if (existentes.Count != ids.Count)
            return ("Uno o más árbitros seleccionados no existen.", ids);

        if (existentes.Any(e => e.Rol != Rol.ARBITRO || !e.Activo))
            return ("Solo se pueden asignar usuarios activos con rol Árbitro.", ids);

        return (null, ids);
    }

    /// <summary>Notifica a cada árbitro asignado (una notificación individual, idempotente).</summary>
    private Task NotificarArbitros(Game game, List<Guid> refereeIds, Guid? adminId) =>
        NotificacionesService.NotificarAsignacion(_db, game, refereeIds, adminId);

    private Guid? GetUserId()
    {
        var sub = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(sub, out var id) ? id : null;
    }
}

public class RescheduleDto
{
    public DateOnly Fecha { get; set; }
    public string Hora { get; set; } = "";
}

public class ResultDto
{
    public int MarcadorLocal { get; set; }
    public int MarcadorVisitante { get; set; }
}

public class CreateGameDto
{
    public Guid SeasonId { get; set; }
    public DateOnly Fecha { get; set; }
    public string Hora { get; set; } = "";
    public Guid HomeTeamId { get; set; }
    public Guid AwayTeamId { get; set; }
    public string? Fase { get; set; }
    public List<Guid>? ArbitroIds { get; set; }
}

public class SetArbitrosDto
{
    // Contrato vigente del frontend
    public List<Guid>? ArbitroIds { get; set; }
    // Alias opcional (mismo contrato, nombre alternativo admitido)
    public List<Guid>? RefereeIds { get; set; }

    /// <summary>Ids efectivos: 'arbitroIds' gana si vienen ambos.</summary>
    public List<Guid>? Efectivos => ArbitroIds ?? RefereeIds;
}

public class ArbitroAsignadoDto
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = "";
}
