using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ambc.Api.Models;
using Ambc.Api.Data;

namespace Ambc.Api.Controllers;

[ApiController]
[Route("api/notificaciones")]
public class NotificationsController : ControllerBase
{
    private readonly AmbcDbContext _db;
    public NotificationsController(AmbcDbContext db) => _db = db;

    // ===== Listar notificaciones para el usuario logueado =====
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetMine()
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var rol = GetRol();

        // Determinar a qué equipos pertenece (coach)
        var teamIds = await _db.Coaches
            .Where(c => c.UserId == userId.Value)
            .Select(c => c.TeamId)
            .ToListAsync();

        var all = await _db.Notifications
            .Where(n => n.Activo)
            .OrderByDescending(n => n.CreadoEn)
            .ToListAsync();

        var result = new List<NotificationDto>();

        foreach (var n in all)
        {
            if (!AplicaA(n.Destinatarios, rol, userId.Value, teamIds))
                continue;

            var vista = await _db.NotificationViews.AnyAsync(v =>
                v.NotificationId == n.Id && v.UserId == userId.Value);

            result.Add(new NotificationDto
            {
                Id = n.Id,
                Mensaje = n.Mensaje,
                Destinatarios = n.Destinatarios,
                CreadoEn = n.CreadoEn,
                Vista = vista,
            });
        }

        return Ok(result);
    }

    // ===== Marcar como vista =====
    [HttpPost("{id}/visto")]
    [Authorize]
    public async Task<IActionResult> MarcarVista(Guid id)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var n = await _db.Notifications.FindAsync(id);
        if (n is null) return NotFound();

        var exists = await _db.NotificationViews.AnyAsync(v =>
            v.NotificationId == id && v.UserId == userId.Value);
        if (!exists)
        {
            _db.NotificationViews.Add(new NotificationView
            {
                NotificationId = id,
                UserId = userId.Value,
                VistoEn = DateTime.UtcNow,
            });
            await _db.SaveChangesAsync();
        }
        return NoContent();
    }

    // ===== Listar notificaciones públicas (FAN, sin auth) =====
    [HttpGet("fan")]
    [AllowAnonymous]
    public async Task<IActionResult> GetFan()
    {
        var all = await _db.Notifications
            .Where(n => n.Activo &&
                (n.Destinatarios == "ALL" || n.Destinatarios == "FAN"))
            .OrderByDescending(n => n.CreadoEn)
            .Select(n => new NotificationDto
            {
                Id = n.Id,
                Mensaje = n.Mensaje,
                Destinatarios = n.Destinatarios,
                CreadoEn = n.CreadoEn,
                Vista = false,
            })
            .ToListAsync();

        return Ok(all);
    }

    // ===== Helpers =====
    private static bool AplicaA(string dest, string rol, Guid userId, List<Guid> teamIds)
    {
        switch (dest)
        {
            case "ALL": return true;
            case "COACH": return rol == "COACH";
            case "ARBITRO": return rol == "ARBITRO";
            case "FAN": return false; // FAN no usa este endpoint
            default:
                if (dest.StartsWith("TEAM:"))
                {
                    var teamIdStr = dest.Substring(5);
                    return Guid.TryParse(teamIdStr, out var tid) && teamIds.Contains(tid);
                }
                if (dest.StartsWith("USER:"))
                {
                    var uidStr = dest.Substring(5);
                    return Guid.TryParse(uidStr, out var uid) && uid == userId;
                }
                return false;
        }
    }

    private Guid? GetUserId()
    {
        var sub = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(sub, out var id) ? id : null;
    }

    private string GetRol()
    {
        return User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "";
    }
}
