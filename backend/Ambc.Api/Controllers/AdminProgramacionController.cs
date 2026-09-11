using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ambc.Api.Data;
using Ambc.Api.Models;
using Ambc.Api.Services;

namespace Ambc.Api.Controllers;

/// <summary>
/// Programación SEMANAL de partidos: la semana es la unidad administrativa.
/// Reemplaza la generación de los 72 partidos de una sola vez.
/// </summary>
[ApiController]
[Route("api/admin/programacion")]
[Authorize(Roles = "ADMIN_AMBC")]
public class AdminProgramacionController : ControllerBase
{
    private readonly AmbcDbContext _db;
    public AdminProgramacionController(AmbcDbContext db) => _db = db;

    // ===== Propuesta automática de la semana =====
    [HttpGet("pendientes")]
    public async Task<IActionResult> Pendientes([FromQuery] Guid seasonId, [FromQuery] int semana)
    {
        if (semana < 1) return BadRequest(new { error = "La semana debe ser 1 o mayor." });

        var (error, propuesta) = await SemanaProgramacion.Proponer(_db, seasonId, semana);
        if (error is not null) return BadRequest(new { error });

        var equipos = await _db.Teams.ToDictionaryAsync(t => t.Id, t => t.Nombre);
        var season = await _db.Seasons.FindAsync(seasonId);

        var rondasPendientes = (await SemanaProgramacion.RondasPendientes(
            _db, seasonId,
            await _db.Teams.Where(t => _db.Rosters.Any(r => r.SeasonId == seasonId && r.TeamId == t.Id))
                .OrderBy(t => t.Nombre).Select(t => t.Id).ToListAsync())).Count;

        return Ok(new
        {
            semana,
            inicio = season is null ? (DateOnly?)null : SemanaProgramacion.InicioDeSemana(season, semana),
            fin = season is null ? (DateOnly?)null : SemanaProgramacion.InicioDeSemana(season, semana).AddDays(6),
            rondasPendientes,
            propuesta = propuesta.Select(p => new
            {
                fecha = p.Fecha,
                hora = p.Hora,
                homeTeamId = p.HomeTeamId,
                awayTeamId = p.AwayTeamId,
                local = equipos.GetValueOrDefault(p.HomeTeamId, "?"),
                visitante = equipos.GetValueOrDefault(p.AwayTeamId, "?"),
                arbitroIds = new List<Guid>(),
            }),
        });
    }

    // ===== Confirmar semana (crea los Games + árbitros + notificaciones) =====
    [HttpPost("semana")]
    public async Task<IActionResult> ConfirmarSemana([FromBody] ConfirmarSemanaDto dto)
    {
        var error = await SemanaProgramacion.ValidarSemana(_db, dto.SeasonId, dto.Semana, dto.Partidos);
        if (error is not null) return BadRequest(new { error });

        var adminId = GetUserId();
        var creados = new List<Guid>();

        foreach (var p in dto.Partidos)
        {
            var game = new Game
            {
                SeasonId = dto.SeasonId,
                Fase = "REGULAR",
                HomeTeamId = p.HomeTeamId,
                AwayTeamId = p.AwayTeamId,
                Fecha = p.Fecha,
                Hora = p.Hora,
                Estado = GameEstado.SCHEDULED,
                Semana = dto.Semana,
            };
            _db.Games.Add(game);
            await _db.SaveChangesAsync();

            var arbitros = p.ArbitroIds ?? new List<Guid>();
            foreach (var refereeId in arbitros)
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

            // Notificación operativa a cada árbitro (idempotente)
            await NotificacionesService.NotificarAsignacion(_db, game, arbitros, adminId);

            creados.Add(game.Id);
        }

        return Ok(new { semana = dto.Semana, partidos = creados.Count, ids = creados });
    }

    // ===== Semanas de una temporada (para el selector) =====
    [HttpGet("semanas")]
    public async Task<IActionResult> Semanas([FromQuery] Guid seasonId)
    {
        var season = await _db.Seasons.FindAsync(seasonId);
        if (season is null) return NotFound(new { error = "Temporada no encontrada." });

        var totalDias = season.FechaFin.DayNumber - season.FechaInicio.DayNumber + 1;
        var totalSemanas = Math.Max(1, (int)Math.Ceiling(totalDias / 7.0));

        var conPartidos = await _db.Games
            .Where(g => g.SeasonId == seasonId)
            .Select(g => g.Semana)
            .Distinct()
            .ToListAsync();

        var jugadas = await _db.Games
            .Where(g => g.SeasonId == seasonId)
            .GroupBy(g => g.Semana)
            .Select(x => new { Semana = x.Key, Total = x.Count() })
            .ToListAsync();

        return Ok(new
        {
            totalSemanas,
            inicioTemporada = season.FechaInicio,
            finTemporada = season.FechaFin,
            semanas = Enumerable.Range(1, totalSemanas).Select(n => new
            {
                semana = n,
                inicio = SemanaProgramacion.InicioDeSemana(season, n),
                fin = SemanaProgramacion.InicioDeSemana(season, n).AddDays(6),
                partidos = jugadas.FirstOrDefault(j => j.Semana == n)?.Total ?? 0,
                programada = conPartidos.Contains(n),
            }),
        });
    }

    private Guid? GetUserId()
    {
        var sub = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(sub, out var id) ? id : null;
    }
}
