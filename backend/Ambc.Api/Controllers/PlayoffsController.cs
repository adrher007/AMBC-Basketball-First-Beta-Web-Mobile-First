using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ambc.Api.Data;
using Ambc.Api.Models;
using Ambc.Api.Services;

namespace Ambc.Api.Controllers;

[ApiController]
[Route("api/admin/playoffs")]
[Authorize(Roles = "ADMIN_AMBC")]
public class PlayoffsController : ControllerBase
{
    private readonly AmbcDbContext _db;
    public PlayoffsController(AmbcDbContext db) => _db = db;

    // Iniciar playoffs (crea series QF + primer partido de cada una)
    [HttpPost("iniciar")]
    public async Task<IActionResult> Iniciar([FromBody] PlayoffIniciarDto dto)
    {
        var season = await _db.Seasons.FindAsync(dto.SeasonId);
        if (season is null) return NotFound(new { error = "Temporada no encontrada." });

        var (ok, error) = await PlayoffService.Iniciar(_db, dto.SeasonId);
        if (!ok) return BadRequest(new { error });

        return Ok(new { mensaje = error });
    }

    // Estado de los playoffs (series + ganador)
    [HttpGet("estado/{seasonId}")]
    [AllowAnonymous]
    public async Task<IActionResult> Estado(Guid seasonId)
    {
        var series = await _db.PlayoffSeries
            .Where(p => p.SeasonId == seasonId)
            .OrderBy(p => p.Fase)
            .Select(p => new
            {
                p.Id,
                Fase = p.Fase.ToString(),
                p.TeamAId,
                p.TeamBId,
                p.WinsA,
                p.WinsB,
                p.BestOf,
                p.GanadorId,
                p.Estado,
            })
            .ToListAsync();

        return Ok(series);
    }
}

public class PlayoffIniciarDto
{
    public Guid SeasonId { get; set; }
}
