using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ambc.Api.Models;
using Ambc.Api.Data;

namespace Ambc.Api.Controllers;

[ApiController]
[Route("api/rosters")]
public class RostersController : ControllerBase
{
    private readonly AmbcDbContext _db;
    public RostersController(AmbcDbContext db) => _db = db;

    // Obtener el roster de un equipo en una temporada (público)
    [HttpGet("equipo/{teamId}/{seasonId}")]
    public async Task<IActionResult> GetByTeam(Guid teamId, Guid seasonId)
    {
        var roster = await _db.Rosters
            .Where(r => r.TeamId == teamId && r.SeasonId == seasonId)
            .Join(_db.Players, r => r.PlayerId, p => p.Id, (r, p) => new RosterDto
            {
                PlayerId = p.Id,
                Nombre = p.Nombre,
                Apellido = p.Apellido,
                Posicion = p.Posicion,
                Numero = r.Numero,
                Activo = r.Activo,
            })
            .OrderBy(x => x.Numero)
            .ToListAsync();
        return Ok(roster);
    }

    // COACH crea un jugador + roster (solo su equipo)
    [HttpPost]
    [Authorize(Roles = "COACH")]
    public async Task<IActionResult> Create([FromBody] RosterCreateDto dto)
    {
        // Obtener el coach actual a partir del claim del usuario
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var coach = await _db.Coaches.FirstOrDefaultAsync(c => c.UserId == userId.Value);
        if (coach is null)
            return Forbid("No tienes un equipo asignado como entrenador.");

        // Validar posición
        if (!Enum.TryParse<Posicion>(dto.Posicion, true, out var pos))
            return BadRequest(new { error = "Posición inválida." });

        // La temporada del coach
        var seasonId = coach.SeasonId;

        // Crear jugador
        var player = new Player
        {
            Nombre = dto.Nombre.Trim(),
            Apellido = dto.Apellido.Trim(),
            Posicion = pos.ToString(),
        };
        _db.Players.Add(player);
        await _db.SaveChangesAsync();

        // Crear roster
        var roster = new Roster
        {
            SeasonId = seasonId,
            TeamId = coach.TeamId,
            PlayerId = player.Id,
            Numero = dto.Numero,
            Activo = true,
        };
        _db.Rosters.Add(roster);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return BadRequest(new { error = "El jugador ya pertenece a un equipo esta temporada." });
        }

        return Created("", new RosterDto
        {
            PlayerId = player.Id,
            Nombre = player.Nombre,
            Apellido = player.Apellido,
            Posicion = player.Posicion,
            Numero = roster.Numero,
            Activo = roster.Activo,
        });
    }

    // COACH cambia el número de un jugador de su roster
    [HttpPut("{playerId}/numero")]
    [Authorize(Roles = "COACH")]
    public async Task<IActionResult> UpdateNumero(Guid playerId, [FromBody] RosterUpdateNumeroDto dto)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var coach = await _db.Coaches.FirstOrDefaultAsync(c => c.UserId == userId.Value);
        if (coach is null) return Forbid();

        var roster = await _db.Rosters.FirstOrDefaultAsync(r =>
            r.PlayerId == playerId && r.TeamId == coach.TeamId && r.SeasonId == coach.SeasonId);
        if (roster is null) return NotFound();

        roster.Numero = dto.Numero;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // COACH elimina un jugador de su roster (solo su equipo)
    [HttpDelete("{playerId}")]
    [Authorize(Roles = "COACH")]
    public async Task<IActionResult> Delete(Guid playerId)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var coach = await _db.Coaches.FirstOrDefaultAsync(c => c.UserId == userId.Value);
        if (coach is null) return Forbid();

        var roster = await _db.Rosters.FirstOrDefaultAsync(r =>
            r.PlayerId == playerId && r.TeamId == coach.TeamId && r.SeasonId == coach.SeasonId);
        if (roster is null)
            return NotFound(new { error = "El jugador no está en tu plantilla." });

        // No eliminar si el jugador tiene stats en partidos (histórico)
        var tieneStats = await _db.PlayerGameStats.AnyAsync(s => s.PlayerId == playerId);
        if (tieneStats)
            return BadRequest(new { error = "No se puede eliminar: el jugador tiene estadísticas registradas." });

        // Borrar roster y el player (si no está en otro roster)
        _db.Rosters.Remove(roster);

        var enOtroRoster = await _db.Rosters.AnyAsync(r => r.PlayerId == playerId && r.Id != roster.Id);
        if (!enOtroRoster)
        {
            var player = await _db.Players.FindAsync(playerId);
            if (player is not null)
                _db.Players.Remove(player);
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }

    private Guid? GetUserId()
    {
        var sub = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(sub, out var id) ? id : null;
    }
}
