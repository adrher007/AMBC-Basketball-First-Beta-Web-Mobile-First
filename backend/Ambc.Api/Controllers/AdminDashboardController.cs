using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ambc.Api.Data;
using Ambc.Api.Models;

namespace Ambc.Api.Controllers;

[ApiController]
[Route("api/admin/dashboard")]
[Authorize(Roles = "ADMIN_AMBC")]
public class AdminDashboardController : ControllerBase
{
    private readonly AmbcDbContext _db;
    public AdminDashboardController(AmbcDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var seasonActiva = await _db.Seasons
            .Where(s => s.Estado == SeasonEstado.ACTIVE)
            .OrderByDescending(s => s.FechaInicio)
            .Select(s => new { s.Id, s.Nombre, Estado = s.Estado.ToString() })
            .FirstOrDefaultAsync();

        var totalEquipos = await _db.Teams.CountAsync();
        var totalJugadores = await _db.Players.CountAsync();
        var totalCoaches = await _db.Coaches.CountAsync();
        var totalArbitros = await _db.Referees.CountAsync();

        int totalPartidos = 0;
        int partidosPendientes = 0;
        if (seasonActiva is not null)
        {
            totalPartidos = await _db.Games.CountAsync(g => g.SeasonId == seasonActiva.Id);
            partidosPendientes = await _db.Games.CountAsync(g =>
                g.SeasonId == seasonActiva.Id && g.Estado == GameEstado.SCHEDULED);
        }

        int avisosActivos = await _db.Notifications.CountAsync(n => n.Activo);

        return Ok(new
        {
            temporadaActiva = seasonActiva,
            totalEquipos,
            totalJugadores,
            totalCoaches,
            totalArbitros,
            totalPartidos,
            partidosPendientes,
            avisosActivos,
        });
    }
}
