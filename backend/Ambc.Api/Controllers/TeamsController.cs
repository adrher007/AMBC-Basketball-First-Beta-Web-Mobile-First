using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ambc.Api.Models;
using Ambc.Api.Data;

namespace Ambc.Api.Controllers;

[ApiController]
[Route("api/teams")]
public class TeamsController : ControllerBase
{
    private readonly AmbcDbContext _db;
    public TeamsController(AmbcDbContext db) => _db = db;

    // Lectura pública
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var teams = await _db.Teams
            .OrderBy(t => t.Nombre)
            .Select(t => new TeamDto
            {
                Id = t.Id,
                Nombre = t.Nombre,
                NombreCorto = t.NombreCorto,
                LogoUrl = t.LogoUrl,
            })
            .ToListAsync();
        return Ok(teams);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var t = await _db.Teams.FindAsync(id);
        if (t is null) return NotFound();
        return Ok(new TeamDto
        {
            Id = t.Id,
            Nombre = t.Nombre,
            NombreCorto = t.NombreCorto,
            LogoUrl = t.LogoUrl,
        });
    }

    // Detalle completo del equipo: info + plantilla (jugadores) + cuerpo técnico
    [HttpGet("{id}/detalle")]
    public async Task<IActionResult> GetDetalle(Guid id)
    {
        var team = await _db.Teams.FindAsync(id);
        if (team is null) return NotFound();

        // Temporada activa (si hay)
        var season = await _db.Seasons
            .Where(s => s.Estado == SeasonEstado.ACTIVE)
            .OrderByDescending(s => s.FechaInicio)
            .FirstOrDefaultAsync();

        Guid? seasonId = season?.Id;
        List<object> plantilla = new();
        List<object> cuerpoTecnico = new();

        if (seasonId is not null)
        {
            plantilla = await _db.Rosters
                .Where(r => r.TeamId == id && r.SeasonId == seasonId && r.Activo)
                .Join(_db.Players, r => r.PlayerId, p => p.Id, (r, p) => new
                {
                    p.Id,
                    p.Nombre,
                    p.Apellido,
                    p.Posicion,
                    r.Numero,
                })
                .OrderBy(x => x.Numero)
                .Select(x => (object)x)
                .ToListAsync();

            cuerpoTecnico = await _db.Coaches
                .Where(c => c.TeamId == id && c.SeasonId == seasonId)
                .Join(_db.Users, c => c.UserId, u => u.Id, (c, u) => new { u.Nombre, Rol = "Entrenador" })
                .Select(x => (object)x)
                .ToListAsync();
        }

        return Ok(new
        {
            Id = team.Id,
            Nombre = team.Nombre,
            NombreCorto = team.NombreCorto,
            LogoUrl = team.LogoUrl,
            Temporada = season?.Nombre,
            Plantilla = plantilla,
            CuerpoTecnico = cuerpoTecnico,
        });
    }

    // Solo ADMIN_AMBC puede crear/editar equipos
    [HttpPost]
    [Authorize(Roles = "ADMIN_AMBC")]
    public async Task<IActionResult> Create([FromBody] TeamCreateDto dto)
    {
        var team = new Team
        {
            Nombre = dto.Nombre.Trim(),
            NombreCorto = dto.NombreCorto.Trim(),
        };
        _db.Teams.Add(team);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = team.Id },
            new TeamDto { Id = team.Id, Nombre = team.Nombre, NombreCorto = team.NombreCorto, LogoUrl = team.LogoUrl });
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "ADMIN_AMBC")]
    public async Task<IActionResult> Update(Guid id, [FromBody] TeamCreateDto dto)
    {
        var t = await _db.Teams.FindAsync(id);
        if (t is null) return NotFound();
        t.Nombre = dto.Nombre.Trim();
        t.NombreCorto = dto.NombreCorto.Trim();
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "ADMIN_AMBC")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var t = await _db.Teams.FindAsync(id);
        if (t is null) return NotFound();

        var hasRoster = await _db.Rosters.AnyAsync(r => r.TeamId == id);
        var hasGames = await _db.Games.AnyAsync(g => g.HomeTeamId == id || g.AwayTeamId == id);
        if (hasRoster || hasGames)
            return BadRequest(new { error = "No se puede eliminar un equipo con plantillas o partidos asociados." });

        _db.Teams.Remove(t);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
