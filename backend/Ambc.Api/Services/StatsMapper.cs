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
        [JsonPropertyName("jugadores_local")] public List<JugadorDto>? JugadoresLocal { get; set; }
        [JsonPropertyName("jugadores_visitante")] public List<JugadorDto>? JugadoresVisitante { get; set; }
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
            data = JsonSerializer.Deserialize<PlanillaJson>(datosCorregidosJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch
        {
            return (0, "JSON de datos corregidos inválido.");
        }

        if (data is null)
            return (0, "JSON de datos corregidos inválido.");

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
                    Reb = 0,
                    Ast = 0,
                    Stl = 0,
                    Blk = 0,
                    To = 0,
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
}
