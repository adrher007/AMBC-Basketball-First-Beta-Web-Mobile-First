using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ambc.Api.Models;
using Ambc.Api.Data;
using Ambc.Api.Services;

namespace Ambc.Api.Controllers;

[ApiController]
[Route("api/admin/referees")]
[Authorize(Roles = "ADMIN_AMBC")]
public class AdminRefereesController : ControllerBase
{
    private readonly AmbcDbContext _db;
    public AdminRefereesController(AmbcDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var refs = await _db.Referees
            .Join(_db.Users, r => r.UserId, u => u.Id, (r, u) => new RefereeDto
            {
                Id = r.Id,
                UserId = r.UserId,
                NombreCompleto = u.Nombre,
                Username = u.Username,
            })
            .OrderBy(r => r.NombreCompleto)
            .ToListAsync();

        return Ok(refs);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] RefereeCreateDto dto)
    {
        var nombre = dto.NombreCompleto.Trim();
        var username = UsernameNormalizer.Normalize(nombre);
        if (username.Length == 0)
            return BadRequest(new { error = "Nombre inválido." });

        if (dto.Password.Length < 4)
            return BadRequest(new { error = "La contraseña debe tener al menos 4 caracteres." });

        // 3.2 — Si el username existe, se sufija: juanperez → juanperez2 → juanperez3
        username = await UsernameNormalizer.UnicoAsync(
            username, u => _db.Users.AnyAsync(x => x.Username == u));

        var adminId = GetUserId();

        var user = new User
        {
            Nombre = nombre,
            Username = username,
            PasswordHash = PasswordHasher.Hash(dto.Password),
            Rol = Rol.ARBITRO,
            Activo = true,
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var referee = new Referee
        {
            UserId = user.Id,
            CreadoPor = adminId ?? user.Id,
        };
        _db.Referees.Add(referee);
        await _db.SaveChangesAsync();

        return Created("", new RefereeDto
        {
            Id = referee.Id,
            UserId = user.Id,
            NombreCompleto = nombre,
            Username = username,
        });
    }

    // ===== Eliminar árbitro (solo si no tiene planillas) =====
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var referee = await _db.Referees.FindAsync(id);
        if (referee is null) return NotFound(new { error = "Árbitro no encontrado." });

        // No eliminar si tiene planillas (rompería la inmutabilidad de planillas confirmadas)
        var tienePlanillas = await _db.Planillas.AnyAsync(p => p.ArbitroId == id);
        if (tienePlanillas)
            return BadRequest(new { error = "No se puede eliminar: el árbitro tiene planillas registradas." });

        var user = await _db.Users.FindAsync(referee.UserId);
        _db.Referees.Remove(referee);
        if (user is not null)
            _db.Users.Remove(user);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    // ===== Reset de contraseña (desbloquear cuenta) =====
    [HttpPut("{id}/password")]
    public async Task<IActionResult> ResetPassword(Guid id, [FromBody] RefereePasswordDto dto)
    {
        var referee = await _db.Referees.FindAsync(id);
        if (referee is null) return NotFound(new { error = "Árbitro no encontrado." });

        if (string.IsNullOrWhiteSpace(dto.Password) || dto.Password.Length < 4)
            return BadRequest(new { error = "La contraseña debe tener al menos 4 caracteres." });

        var user = await _db.Users.FindAsync(referee.UserId);
        if (user is null) return NotFound(new { error = "Usuario no encontrado." });

        user.PasswordHash = PasswordHasher.Hash(dto.Password);
        user.Activo = true;
        await _db.SaveChangesAsync();

        return Ok(new { mensaje = "Contraseña actualizada." });
    }

    private Guid? GetUserId()
    {
        var sub = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(sub, out var id) ? id : null;
    }
}
