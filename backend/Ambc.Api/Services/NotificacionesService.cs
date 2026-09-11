using Microsoft.EntityFrameworkCore;
using Ambc.Api.Data;
using Ambc.Api.Models;

namespace Ambc.Api.Services;

/// <summary>
/// Dos tipos de notificación, nunca confundidos:
///  - PARTIDO_HOY  → informativa, destinatario COACH. Se genera cuando llega la fecha del Game.
///  - ASIGNACION   → operativa para el árbitro. Se genera al asignar árbitros (GameReferees).
///
/// Idempotencia: cada evento queda registrado en NotificationEvents con
/// UNIQUE(game_id, tipo, fecha). Ejecutar el proceso varias veces no duplica nada.
/// FAN no recibe notificaciones individuales (no tiene usuario en BD); la
/// información de partidos es pública en la interfaz.
/// </summary>
public static class NotificacionesService
{
    public const string TipoPartidoHoy = "PARTIDO_HOY";
    public const string TipoAsignacion = "ASIGNACION";

    /// <summary>
    /// Notifica a cada árbitro su asignación (individual) — operativa.
    /// Idempotente POR ÁRBITRO: en una re-asignación solo reciben notificación los nuevos.
    /// </summary>
    public static async Task<int> NotificarAsignacion(
        AmbcDbContext db, Game game, List<Guid> refereeIds, Guid? adminId)
    {
        if (refereeIds.Count == 0) return 0;

        var equipos = await db.Teams.ToDictionaryAsync(t => t.Id, t => t.Nombre);
        var local = equipos.GetValueOrDefault(game.HomeTeamId, "Equipo A");
        var visitante = equipos.GetValueOrDefault(game.AwayTeamId, "Equipo B");

        var mensaje = $"Nuevo partido asignado. Se te ha asignado como árbitro para el partido {local} vs {visitante}. " +
                      $"{game.Fecha:dd/MM/yyyy} — {game.Hora}";

        // Árbitros que ya tienen evento ASIGNACION para este partido/fecha
        var yaNotificados = await db.NotificationEvents
            .Where(ne => ne.GameId == game.Id && ne.Tipo == TipoAsignacion && ne.Fecha == game.Fecha)
            .Select(ne => ne.RefereeId)
            .ToListAsync();

        var pendientes = refereeIds.Where(r => !yaNotificados.Contains(r)).Distinct().ToList();
        if (pendientes.Count == 0) return 0;

        var refs = await db.Referees
            .Where(r => pendientes.Contains(r.Id))
            .Select(r => new { r.Id, r.UserId })
            .ToListAsync();

        var creadas = 0;
        foreach (var r in refs)
        {
            var n = new Notification
            {
                Mensaje = mensaje,
                Destinatarios = $"USER:{r.UserId}",
                CreadoPor = adminId ?? Guid.Empty,
                Activo = true,
                CreadoEn = DateTime.UtcNow,
            };
            db.Notifications.Add(n);
            await db.SaveChangesAsync();

            db.NotificationEvents.Add(new NotificationEvent
            {
                GameId = game.Id,
                Tipo = TipoAsignacion,
                Fecha = game.Fecha,
                RefereeId = r.Id,
                NotificationId = n.Id,
            });
            await db.SaveChangesAsync();
            creadas++;
        }

        return creadas;
    }

    /// <summary>
    /// Notificaciones de partido del día (informativas, destinatario COACH).
    /// Idempotente: registra la fecha del Game, así el proceso puede ejecutarse
    /// muchas veces (o reiniciarse el servidor) sin duplicar.
    /// </summary>
    public static async Task<int> NotificarPartidosDelDia(AmbcDbContext db, DateOnly? fecha = null)
    {
        var hoy = fecha ?? DateOnly.FromDateTime(DateTime.Now);

        var juegos = await db.Games
            .Where(g => g.Fecha == hoy)
            .Select(g => new { g.Id, g.HomeTeamId, g.AwayTeamId, g.Hora, g.Fecha })
            .ToListAsync();

        if (juegos.Count == 0) return 0;

        var equipos = await db.Teams.ToDictionaryAsync(t => t.Id, t => t.NombreCorto);
        var admin = await db.Users
            .Where(u => u.Rol == Rol.ADMIN_AMBC)
            .Select(u => u.Id)
            .FirstOrDefaultAsync();

        var creadas = 0;
        foreach (var g in juegos)
        {
            var yaExiste = await db.NotificationEvents
                .AnyAsync(ne => ne.GameId == g.Id && ne.Tipo == TipoPartidoHoy
                    && ne.Fecha == g.Fecha && ne.RefereeId == Guid.Empty);
            if (yaExiste) continue;

            var local = equipos.GetValueOrDefault(g.HomeTeamId, "?");
            var visitante = equipos.GetValueOrDefault(g.AwayTeamId, "?");

            // 1) Informativa para COACH (una por partido)
            var n = new Notification
            {
                Mensaje = $"Hoy hay partido — {local} vs {visitante}, {g.Hora}",
                Destinatarios = "COACH",
                CreadoPor = admin,
                Activo = true,
                CreadoEn = DateTime.UtcNow,
            };
            db.Notifications.Add(n);
            await db.SaveChangesAsync();

            // 2) Misma información a cada árbitro asignado (operativa + informativa).
            //    Un árbitro puede recibir tanto esta como la de asignación.
            var arbitrosUsuario = await db.GameReferees
                .Where(gr => gr.GameId == g.Id)
                .Join(db.Referees, gr => gr.RefereeId, r => r.Id, (gr, r) => r.UserId)
                .ToListAsync();

            foreach (var userId in arbitrosUsuario)
            {
                db.Notifications.Add(new Notification
                {
                    Mensaje = $"Hoy hay partido — {local} vs {visitante}, {g.Hora}",
                    Destinatarios = $"USER:{userId}",
                    CreadoPor = admin,
                    Activo = true,
                    CreadoEn = DateTime.UtcNow,
                });
                await db.SaveChangesAsync();
            }

            db.NotificationEvents.Add(new NotificationEvent
            {
                GameId = g.Id,
                Tipo = TipoPartidoHoy,
                Fecha = g.Fecha,
                NotificationId = n.Id,
            });
            await db.SaveChangesAsync();
            creadas++;
        }

        return creadas;
    }
}
