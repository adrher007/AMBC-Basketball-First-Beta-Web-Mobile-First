using System.ComponentModel.DataAnnotations;

namespace Ambc.Api.Models;

// Resultado del procesamiento (respuesta al árbitro tras subir imagen)
public class PlanillaDto
{
    public Guid Id { get; set; }
    public Guid GameId { get; set; }
    public string Estado { get; set; } = "";
    public string? ImagenUrl { get; set; }
    public string? DatosExtraidosJson { get; set; }
    public string? DatosCorregidosJson { get; set; }
    public string? Error { get; set; }
    public string? MarcadorLocal { get; set; }
    public string? MarcadorVisitante { get; set; }
    // Planilla compartida entre los árbitros asignados
    public string? CreadaPor { get; set; }
    public bool Compartida { get; set; }
}

public class ConfirmarPlanillaDto
{
    [Required]
    public int MarcadorLocal { get; set; }
    [Required]
    public int MarcadorVisitante { get; set; }
    // JSON corregido por el árbitro (jugadores + stats), opcional
    public string? DatosCorregidosJson { get; set; }
}
