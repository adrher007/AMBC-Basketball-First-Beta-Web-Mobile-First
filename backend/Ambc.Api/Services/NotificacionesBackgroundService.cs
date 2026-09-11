using Ambc.Api.Data;

namespace Ambc.Api.Services;

/// <summary>
/// Revisa periódicamente si hay partidos hoy y genera la notificación informativa
/// (destinatario COACH) una sola vez por partido y fecha.
/// Se ejecuta al arrancar el servidor y cada hora — sobrevive a reinicios porque
/// la idempotencia vive en la tabla NotificationEvents.
/// </summary>
public class NotificacionesBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NotificacionesBackgroundService> _log;

    public NotificacionesBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<NotificacionesBackgroundService> log)
    {
        _scopeFactory = scopeFactory;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Primera pasada al arrancar (por si el servidor estuvo apagado)
        await Ejecutar(stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await Ejecutar(stoppingToken);
        }
    }

    private async Task Ejecutar(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AmbcDbContext>();
            var creadas = await NotificacionesService.NotificarPartidosDelDia(db);
            if (creadas > 0)
                _log.LogInformation("Notificaciones 'Hoy hay partido' creadas: {Creadas}", creadas);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "No se pudieron generar las notificaciones de partidos del día.");
        }
    }
}
