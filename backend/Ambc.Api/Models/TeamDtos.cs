using System.ComponentModel.DataAnnotations;

namespace Ambc.Api.Models;

public class TeamDto
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = "";
    public string NombreCorto { get; set; } = "";
    public string? LogoUrl { get; set; }
}

public class TeamCreateDto
{
    [Required]
    public string Nombre { get; set; } = "";
    [Required]
    public string NombreCorto { get; set; } = "";
}
