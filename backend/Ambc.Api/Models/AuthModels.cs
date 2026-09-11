using System.ComponentModel.DataAnnotations;

namespace Ambc.Api.Models;

public class LoginRequest
{
    [Required]
    public string Username { get; set; } = "";
    [Required]
    public string Password { get; set; } = "";
}

public class LoginResponse
{
    public string Token { get; set; } = "";
    public string Username { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string Rol { get; set; } = "";
}
