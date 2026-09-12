using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ambc.Api.Models;
using Ambc.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Ambc.Api.Services;

/// <summary>
/// Mapea los datos corregidos de la planilla (jugadores por número) al roster real
/// y escribe las stats en player_game_stats. PTS = 2P×2 + 3P×3 + FT (backend).
/// </summary>
public static class StatsMapper
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private class JugadorDto
    {
        [JsonPropertyName("numero")] public int? Numero { get; set; }
        [JsonPropertyName("nombre")] public string? Nombre { get; set; }
        [JsonPropertyName("apellido")] public string? Apellido { get; set; }
        // Puntos totales del jugador (la planilla AMBC solo registra puntos vía casillas)
        [JsonPropertyName("pts")] public int? Pts { get; set; }
        // Compatibilidad: si viniera el formato viejo con dos_p/tres_p/ft
        [JsonPropertyName("dos_p")] public int? DosP { get; set; }
        [JsonPropertyName("tres_p")] public int? TresP { get; set; }
        [JsonPropertyName("ft")] public int? Ft { get; set; }
    }

    private class PlanillaJson
    {
        // 3.1 — Versión del esquema del JSON (ausente = 1)
        [JsonPropertyName("schemaVersion")] public int? SchemaVersion { get; set; }
        [JsonPropertyName("jugadores_local")] public List<JugadorDto>? JugadoresLocal { get; set; }
        [JsonPropertyName("jugadores_visitante")] public List<JugadorDto>? JugadoresVisitante { get; set; }
    }

    /// <summary>Versión de esquema soportada por este backend.</summary>
    public const int SchemaVersionActual = 1;

    /// <summary>3.1 — Valida la versión de esquema. Devuelve error si es más nueva que la soportada.</summary>
    private static string? ValidarSchema(int? version)
    {
        var v = version ?? SchemaVersionActual;   // ausente → 1
        return v > SchemaVersionActual
            ? $"schemaVersion {v} no soportada por este backend (máximo {SchemaVersionActual}). Actualiza el servidor."
            : null;
    }

    /// <summary>
    /// Procesa datosCorregidosJson y escribe player_game_stats.
    /// Devuelve (cantidad escrita, error). Si error no vacío, no se escribe nada.
    /// </summary>
    public static async Task<(int escritos, string error)> EscribirStats(
        AmbcDbContext db,
        Guid seasonId,
        Guid gameId,
        Guid homeTeamId,
        Guid awayTeamId,
        string? datosCorregidosJson)
    {
        if (string.IsNullOrWhiteSpace(datosCorregidosJson))
            return (0, ""); // sin stats: no es obligatorio (modo degradado)

        PlanillaJson? data;
        try
        {
            data = JsonSerializer.Deserialize<PlanillaJson>(datosCorregidosJson, JsonOpts);
        }
        catch
        {
            return (0, "JSON de datos corregidos inválido.");
        }

        if (data is null)
            return (0, "JSON de datos corregidos inválido.");

        // 3.1 — Rechazar esquemas más nuevos que los soportados
        var errSchema = ValidarSchema(data.SchemaVersion);
        if (errSchema is not null)
            return (0, errSchema);

        // Roster de la season
        var roster = await db.Rosters
            .Where(r => r.SeasonId == seasonId)
            .ToListAsync();

        var stats = new List<PlayerGameStats>();
        var errores = new List<string>();

        void ProcesarEquipo(List<JugadorDto>? jugadores, Guid teamId, string equipoNombre)
        {
            if (jugadores is null) return;
            foreach (var j in jugadores)
            {
                if (j.Numero is null)
                {
                    errores.Add($"Jugador de {equipoNombre} sin número.");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(j.Nombre) || string.IsNullOrWhiteSpace(j.Apellido))
                {
                    errores.Add($"Jugador #{j.Numero} de {equipoNombre} sin nombre/apellido.");
                    continue;
                }

                // Buscar en roster por número + team
                var entry = roster.FirstOrDefault(r =>
                    r.TeamId == teamId && r.Numero == j.Numero.Value);

                if (entry is null)
                {
                    errores.Add($"El número #{j.Numero} ({j.Nombre} {j.Apellido}) no está en el roster de {equipoNombre}.");
                    continue;
                }

                // PTS del jugador: viene explícito (formato nuevo) o se calcula del viejo (2P×2+3P×3+FT)
                int pts;
                int dosP = 0, tresP = 0, ft = 0;
                if (j.Pts is not null)
                {
                    // 1.4 — Si además vienen componentes, deben coincidir con los puntos declarados
                    if (j.DosP is not null || j.TresP is not null || j.Ft is not null)
                    {
                        dosP = j.DosP ?? 0;
                        tresP = j.TresP ?? 0;
                        ft = j.Ft ?? 0;
                        var esperado = dosP * 2 + tresP * 3 + ft;
                        if (esperado != j.Pts.Value)
                            throw new ValidationException(
                                $"Puntos inconsistentes en #{j.Numero} ({equipoNombre}): declarado {j.Pts.Value} ≠ 2P×2 + 3P×3 + FT = {esperado}.");
                    }
                    pts = j.Pts.Value;
                }
                else
                {
                    dosP = j.DosP ?? 0;
                    tresP = j.TresP ?? 0;
                    ft = j.Ft ?? 0;
                    pts = dosP * 2 + tresP * 3 + ft;
                }

                stats.Add(new PlayerGameStats
                {
                    GameId = gameId,
                    PlayerId = entry.PlayerId,
                    TeamId = teamId,
                    DosP = dosP,
                    TresP = tresP,
                    Ft = ft,
                    Pts = pts,
                });
            }
        }

        ProcesarEquipo(data.JugadoresLocal, homeTeamId, "local");
        ProcesarEquipo(data.JugadoresVisitante, awayTeamId, "visitante");

        if (errores.Count > 0)
            return (0, string.Join(" ", errores));

        if (stats.Count == 0)
            return (0, "");

        db.PlayerGameStats.AddRange(stats);
        await db.SaveChangesAsync();
        return (stats.Count, "");
    }

    /// <summary>
    /// 1.5 — Suma los puntos declarados por equipo (para validar contra el marcador).
    /// Devuelve null si el JSON no es válido.
    /// </summary>
    public static (int Local, int Visitante)? SumarPuntos(string? datosCorregidosJson)
    {
        if (string.IsNullOrWhiteSpace(datosCorregidosJson)) return null;

        PlanillaJson? data;
        try
        {
            data = JsonSerializer.Deserialize<PlanillaJson>(datosCorregidosJson, JsonOpts);
        }
        catch
        {
            return null;
        }
        if (data is null) return null;

        // 3.1 — Esquema no soportado: no hay sumas fiables que comparar
        if (ValidarSchema(data.SchemaVersion) is not null) return null;

        static int Sumar(List<JugadorDto>? jugadores) =>
            jugadores?.Sum(j => j.Pts ?? ((j.DosP ?? 0) * 2 + (j.TresP ?? 0) * 3 + (j.Ft ?? 0))) ?? 0;

        return (Sumar(data.JugadoresLocal), Sumar(data.JugadoresVisitante));
    }
}
