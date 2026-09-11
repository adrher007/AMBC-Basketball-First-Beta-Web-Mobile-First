using System.ComponentModel.DataAnnotations;

namespace Ambc.Api.Models;

// ===== Seasons =====
public class SeasonDto
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = "";
    public DateOnly FechaInicio { get; set; }
    public DateOnly FechaFin { get; set; }
    public string Estado { get; set; } = "";
}

public class SeasonCreateDto
{
    [Required]
    public string Nombre { get; set; } = "";
    [Required]
    public DateOnly FechaInicio { get; set; }
    [Required]
    public DateOnly FechaFin { get; set; }
    // Estado inicial opcional: UPCOMING (default) | ACTIVE | FINISHED
    public string? Estado { get; set; }
}

public class SeasonUpdateDto
{
    [Required]
    public string Nombre { get; set; } = "";
    public DateOnly FechaInicio { get; set; }
    public DateOnly FechaFin { get; set; }
}
