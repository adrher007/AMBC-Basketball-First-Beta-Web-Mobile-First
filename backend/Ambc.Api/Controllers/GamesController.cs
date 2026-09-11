using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ambc.Api.Models;
using Ambc.Api.Data;

namespace Ambc.Api.Controllers;

[ApiController]
[Route("api/games")]
public class GamesController : ControllerBase
{
    private readonly AmbcDbContext _db;
    public GamesController(AmbcDbContext db) => _db = db;

    // Listar partidos (público). Filtro opcional por temporada.
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid? seasonId)
    {
        var q = _db.Games.AsQueryable();
        if (seasonId is not null)
            q = q.Where(g => g.SeasonId == seasonId.Value);

        var games = await q
            .OrderBy(g => g.Fecha)
            .ThenBy(g => g.Hora)
            .Select(g => new
            {
                g.Id,
                g.SeasonId,
                g.Fase,
                g.PlayoffSeriesId,
                g.HomeTeamId,
                g.AwayTeamId,
                g.Fecha,
                g.Hora,
                Estado = g.Estado.ToString(),
                g.MarcadorLocal,
                g.MarcadorVisitante,
                g.GanadorId,
            })
            .ToListAsync();

        return Ok(games);
    }
}
