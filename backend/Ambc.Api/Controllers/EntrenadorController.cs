using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ambc.Api.Data;

namespace Ambc.Api.Controllers;

[ApiController]
[Route("api/entrenador")]
[Authorize(Roles = "COACH")]
public class EntrenadorController : ControllerBase
{
    private readonly AmbcDbContext _db;
    private readonly IWebHostEnvironment _env;

    public EntrenadorController(AmbcDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    // Devuelve el team + season asignados al coach logueado
    [HttpGet("mi-equipo")]
    public async Task<IActionResult> MiEquipo()
    {
        var sub = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (sub is null || !Guid.TryParse(sub, out var userId))
            return Unauthorized();

        var coach = await _db.Coaches.FirstOrDefaultAsync(c => c.UserId == userId);
        if (coach is null) return NotFound(new { error = "No tienes un equipo asignado." });

        var team = await _db.Teams.FindAsync(coach.TeamId);
        var season = await _db.Seasons.FindAsync(coach.SeasonId);

        return Ok(new
        {
            TeamId = coach.TeamId,
            TeamNombre = team?.Nombre ?? "",
            TeamNombreCorto = team?.NombreCorto ?? "",
            LogoUrl = team?.LogoUrl,
            SeasonId = coach.SeasonId,
            SeasonNombre = season?.Nombre ?? "",
        });
    }

    // Subir logo del equipo (solo el coach de ese equipo)
    [HttpPost("logo")]
    [RequestSizeLimit(5 * 1024 * 1024)] // 5 MB
    public async Task<IActionResult> SubirLogo(IFormFile file)
    {
        var sub = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (sub is null || !Guid.TryParse(sub, out var userId))
            return Unauthorized();

        var coach = await _db.Coaches.FirstOrDefaultAsync(c => c.UserId == userId);
        if (coach is null) return NotFound(new { error = "No tienes un equipo asignado." });

        var team = await _db.Teams.FindAsync(coach.TeamId);
        if (team is null) return NotFound(new { error = "Equipo no encontrado." });

        if (file is null || file.Length == 0)
            return BadRequest(new { error = "Archivo requerido." });

        if (file.Length > 5 * 1024 * 1024)
            return BadRequest(new { error = "El archivo no puede superar 5 MB." });

        // Validar extensión (PNG/JPG/SVG)
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext is not (".png" or ".jpg" or ".jpeg" or ".svg"))
            return BadRequest(new { error = "Formato no permitido. Usa PNG, JPG o SVG." });

        // Guardar en /uploads/logos/{team_id}/logo.png (normalizado a la extensión real)
        var uploadsDir = Path.Combine(_env.ContentRootPath, "uploads", "logos", coach.TeamId.ToString());
        Directory.CreateDirectory(uploadsDir);

        var extFinal = ext == ".jpeg" ? ".jpg" : ext;
        var filename = $"logo{extFinal}";
        var filePath = Path.Combine(uploadsDir, filename);

        using (var stream = System.IO.File.Create(filePath))
        {
            await file.CopyToAsync(stream);
        }

        // Borrar logos viejos con otra extensión (evita acumular logo.png + logo.jpg)
        foreach (var old in Directory.GetFiles(uploadsDir, "logo.*")
                     .Where(f => !f.EndsWith(filename, StringComparison.OrdinalIgnoreCase)))
        {
            System.IO.File.Delete(old);
        }

        // Actualizar el team
        team.LogoUrl = $"/uploads/logos/{coach.TeamId}/{filename}";
        team.LogoActualizadoPor = userId;
        await _db.SaveChangesAsync();

        return Ok(new { logoUrl = team.LogoUrl });
    }
}
