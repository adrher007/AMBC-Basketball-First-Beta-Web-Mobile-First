using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ambc.Api.Data;

namespace Ambc.Api.Controllers;

[ApiController]
[Route("api/standings")]
public class StandingsController : ControllerBase
{
    private readonly AmbcDbContext _db;
    public StandingsController(AmbcDbContext db) => _db = db;

    // Clasificación pública por temporada (incluye nombre del equipo)
    [HttpGet("{seasonId}")]
    public async Task<IActionResult> Get(Guid seasonId)
    {
        var rows = await _db.Standings
            .Where(s => s.SeasonId == seasonId)
            .OrderBy(s => s.Posicion)
            .Join(_db.Teams, s => s.TeamId, t => t.Id, (s, t) => new
            {
                Posicion = s.Posicion,
                Equipo = t.Nombre,
                NombreCorto = t.NombreCorto,
                s.Pj,
                s.Victorias,
                s.Derrotas,
                Pts = s.Victorias * 2 + s.Derrotas,
                s.Pf,
                s.Pc,
                s.Diff,
            })
            .ToListAsync();

        return Ok(rows);
    }
}
