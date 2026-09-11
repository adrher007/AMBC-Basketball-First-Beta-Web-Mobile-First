using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ambc.Api.Data;
using Ambc.Api.Models;
using Ambc.Api.Services;

namespace Ambc.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AmbcDbContext _db;
    private readonly JwtService _jwt;

    public AuthController(AmbcDbContext db, JwtService jwt)
    {
        _db = db;
        _jwt = jwt;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Username == req.Username.Trim());

        if (user is null || !user.Activo || !PasswordHasher.Verify(req.Password, user.PasswordHash))
            return Unauthorized(new { error = "Credenciales inválidas o usuario inactivo." });

        var token = _jwt.Generate(user);
        return Ok(new LoginResponse
        {
            Token = token,
            Username = user.Username,
            Nombre = user.Nombre,
            Rol = user.Rol.ToString(),
        });
    }

    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var sub = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (sub is null || !Guid.TryParse(sub, out var id))
            return Unauthorized();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id && u.Activo);
        if (user is null) return Unauthorized();

        return Ok(new { user.Username, user.Nombre, Rol = user.Rol.ToString() });
    }
}
