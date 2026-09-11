using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ambc.Api.Models;
using Ambc.Api.Data;
using Ambc.Api.Services;

namespace Ambc.Api.Controllers;

[ApiController]
[Route("api/admin/coaches")]
[Authorize(Roles = "ADMIN_AMBC")]
public class AdminCoachesController : ControllerBase
{
    private readonly AmbcDbContext _db;
    public AdminCoachesController(AmbcDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var coaches = await _db.Coaches
            .Join(_db.Users, c => c.UserId, u => u.Id, (c, u) => new { c, u })
            .Join(_db.Teams, x => x.c.TeamId, t => t.Id, (x, t) => new CoachDto
            {
                Id = x.c.Id,
                UserId = x.c.UserId,
                NombreCompleto = x.u.Nombre,
                Username = x.u.Username,
                TeamId = x.c.TeamId,
                TeamNombre = t.Nombre,
                SeasonId = x.c.SeasonId,
            })
            .OrderBy(c => c.TeamNombre)
            .ToListAsync();

        return Ok(coaches);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CoachCreateDto dto)
    {
        var nombre = dto.NombreCompleto.Trim();
        var username = UsernameNormalizer.Normalize(nombre);
        if (username.Length == 0)
            return BadRequest(new { error = "Nombre inválido." });

        // 3.2 — Si el username existe, se sufija: juanperez → juanperez2 → juanperez3
        username = await UsernameNormalizer.UnicoAsync(
            username, u => _db.Users.AnyAsync(x => x.Username == u));

        // ¿Ya hay un coach para ese (team, season)?
        if (await _db.Coaches.AnyAsync(c => c.TeamId == dto.TeamId && c.SeasonId == dto.SeasonId))
            return BadRequest(new { error = "Ya existe un entrenador para ese equipo en esa temporada." });

        // ¿El team tiene roster en esa season? (requisito razonable, no bloqueante)
        var teamExists = await _db.Teams.AnyAsync(t => t.Id == dto.TeamId);
        if (!teamExists) return BadRequest(new { error = "Equipo no encontrado." });

        var user = new User
        {
            Nombre = nombre,
            Username = username,
            PasswordHash = PasswordHasher.Hash("coach123"),
            Rol = Rol.COACH,
            Activo = true,
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var coach = new Coach
        {
            UserId = user.Id,
            TeamId = dto.TeamId,
            SeasonId = dto.SeasonId,
        };
        _db.Coaches.Add(coach);
        await _db.SaveChangesAsync();

        return Created("", new CoachDto
        {
            Id = coach.Id,
            UserId = user.Id,
            NombreCompleto = nombre,
            Username = username,
            TeamId = dto.TeamId,
            TeamNombre = (await _db.Teams.FindAsync(dto.TeamId))!.Nombre,
            SeasonId = dto.SeasonId,
        });
    }

    // ===== Eliminar entrenador =====
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var coach = await _db.Coaches.FindAsync(id);
        if (coach is null) return NotFound(new { error = "Entrenador no encontrado." });

        var user = await _db.Users.FindAsync(coach.UserId);
        _db.Coaches.Remove(coach);
        if (user is not null)
            _db.Users.Remove(user);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    // ===== Reset de contraseña =====
    [HttpPut("{id}/password")]
    public async Task<IActionResult> ResetPassword(Guid id, [FromBody] CoachPasswordDto dto)
    {
        var coach = await _db.Coaches.FindAsync(id);
        if (coach is null) return NotFound(new { error = "Entrenador no encontrado." });

        if (string.IsNullOrWhiteSpace(dto.Password) || dto.Password.Length < 4)
            return BadRequest(new { error = "La contraseña debe tener al menos 4 caracteres." });

        var user = await _db.Users.FindAsync(coach.UserId);
        if (user is null) return NotFound(new { error = "Usuario no encontrado." });

        user.PasswordHash = PasswordHasher.Hash(dto.Password);
        user.Activo = true;
        await _db.SaveChangesAsync();

        return Ok(new { mensaje = "Contraseña actualizada." });
    }
}
