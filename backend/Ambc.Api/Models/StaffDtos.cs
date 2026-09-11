using System.ComponentModel.DataAnnotations;

namespace Ambc.Api.Models;

public class CoachCreateDto
{
    [Required]
    public string NombreCompleto { get; set; } = "";
    [Required]
    public Guid TeamId { get; set; }
    [Required]
    public Guid SeasonId { get; set; }
}

public class CoachPasswordDto
{
    [Required]
    public string Password { get; set; } = "";
}

public class CoachDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string NombreCompleto { get; set; } = "";
    public string Username { get; set; } = "";
    public Guid TeamId { get; set; }
    public string TeamNombre { get; set; } = "";
    public Guid SeasonId { get; set; }
}

public class RefereeCreateDto
{
    [Required]
    public string NombreCompleto { get; set; } = "";
    [Required]
    public string Password { get; set; } = "";
}

public class RefereePasswordDto
{
    [Required]
    public string Password { get; set; } = "";
}

public class RefereeDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string NombreCompleto { get; set; } = "";
    public string Username { get; set; } = "";
}
