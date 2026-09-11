using System.ComponentModel.DataAnnotations;

namespace Ambc.Api.Models;

// ===== Players =====
public class PlayerDto
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = "";
    public string Apellido { get; set; } = "";
    public string Posicion { get; set; } = "";
}

// ===== Roster =====
public class RosterDto
{
    public Guid PlayerId { get; set; }
    public string Nombre { get; set; } = "";
    public string Apellido { get; set; } = "";
    public string Posicion { get; set; } = "";
    public int Numero { get; set; }
    public bool Activo { get; set; }
}

// El coach crea jugador + roster a la vez
public class RosterCreateDto
{
    [Required]
    public string Nombre { get; set; } = "";
    [Required]
    public string Apellido { get; set; } = "";
    [Required]
    public string Posicion { get; set; } = "";   // BASE|ESCOLTA|ALERO|ALA_PIVOT|PIVOT
    [Required]
    public int Numero { get; set; }
}

public class RosterUpdateNumeroDto
{
    public int Numero { get; set; }
}
