using System.ComponentModel.DataAnnotations;

namespace Ambc.Api.Models;

public class FixtureRequest
{
    [Required]
    public Guid SeasonId { get; set; }
    [Required]
    public DateOnly FechaInicio { get; set; }
    [Required]
    public DateOnly FechaFin { get; set; }
    [Required]
    public string RangoHoras { get; set; } = ""; // "19:00 - 22:00"
}
