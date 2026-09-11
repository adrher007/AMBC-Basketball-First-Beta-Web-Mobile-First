using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ambc.Api.Models;
using Ambc.Api.Data;
using Ambc.Api.Services;

namespace Ambc.Api.Controllers;

[ApiController]
[Route("api/admin/notificaciones")]
[Authorize(Roles = "ADMIN_AMBC")]
public class AdminNotificationsController : ControllerBase
{
    private readonly AmbcDbContext _db;
    public AdminNotificationsController(AmbcDbContext db) => _db = db;

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] NotificationCreateDto dto)
    {
        var resultado = await CrearAsync(dto);
        return resultado;
    }

    // ===== Procesar partidos del día (idempotente) =====
    // Genera la notificación informativa "Hoy hay partido" para COACH una sola
    // vez por partido y fecha. Puede ejecutarse repetidamente sin duplicar.
    [HttpPost("procesar-hoy")]
    public async Task<IActionResult> ProcesarHoy()
    {
        var creadas = await NotificacionesService.NotificarPartidosDelDia(_db);
        var total = await _db.Notifications
            .CountAsync(n => EF.Functions.Like(n.Mensaje, "Hoy hay partido%"));
        return Ok(new { creadas, total });
    }

    private async Task<IActionResult> CrearAsync(NotificationCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Mensaje))
            return BadRequest(new { error = "Mensaje requerido." });

        var destinatarios = dto.Destinatarios.Trim().ToUpperInvariant();
        if (!EsValido(destinatarios))
            return BadRequest(new { error = "Destinatario inválido." });

        var userId = GetUserId();

        var n = new Notification
        {
            Mensaje = dto.Mensaje.Trim(),
            Destinatarios = destinatarios,
            CreadoPor = userId ?? Guid.Empty,
            Activo = true,
            CreadoEn = DateTime.UtcNow,
        };
        _db.Notifications.Add(n);
        await _db.SaveChangesAsync();

        return Ok(new { n.Id, n.Mensaje, n.Destinatarios });
    }

    private static bool EsValido(string d)
    {
        if (d is "ALL" or "COACH" or "FAN" or "ARBITRO") return true;
        if (d.StartsWith("TEAM:")) return Guid.TryParse(d.Substring(5), out _);
        if (d.StartsWith("USER:")) return Guid.TryParse(d.Substring(5), out _);
        return false;
    }

    private Guid? GetUserId()
    {
        var sub = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(sub, out var id) ? id : null;
    }
}
