using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ambc.Api.Models;
using Ambc.Api.Data;

namespace Ambc.Api.Controllers;

[ApiController]
[Route("api/seasons")]
public class SeasonsController : ControllerBase
{
    private readonly AmbcDbContext _db;
    public SeasonsController(AmbcDbContext db) => _db = db;

    // Lectura pública (FAN / consulta)
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var seasons = await _db.Seasons
            .OrderByDescending(s => s.FechaInicio)
            .Select(s => new SeasonDto
            {
                Id = s.Id,
                Nombre = s.Nombre,
                FechaInicio = s.FechaInicio,
                FechaFin = s.FechaFin,
                Estado = s.Estado.ToString(),
            })
            .ToListAsync();
        return Ok(seasons);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var s = await _db.Seasons.FindAsync(id);
        if (s is null) return NotFound();
        return Ok(new SeasonDto
        {
            Id = s.Id,
            Nombre = s.Nombre,
            FechaInicio = s.FechaInicio,
            FechaFin = s.FechaFin,
            Estado = s.Estado.ToString(),
        });
    }

    // Solo ADMIN_AMBC
    [HttpPost]
    [Authorize(Roles = "ADMIN_AMBC")]
    public async Task<IActionResult> Create([FromBody] SeasonCreateDto dto)
    {
        if (dto.FechaFin <= dto.FechaInicio)
            return BadRequest(new { error = "La fecha de fin debe ser posterior a la de inicio." });

        // Estado inicial (opcional, default UPCOMING)
        var estado = SeasonEstado.UPCOMING;
        if (!string.IsNullOrWhiteSpace(dto.Estado))
        {
            if (!Enum.TryParse<SeasonEstado>(dto.Estado, true, out estado))
                return BadRequest(new { error = "Estado inválido. Usa UPCOMING, ACTIVE o FINISHED." });
        }

        if (estado == SeasonEstado.ACTIVE &&
            await _db.Seasons.AnyAsync(x => x.Estado == SeasonEstado.ACTIVE))
            return BadRequest(new { error = "Ya existe una temporada activa. Finalízala primero." });

        var season = new Season
        {
            Nombre = dto.Nombre.Trim(),
            FechaInicio = dto.FechaInicio,
            FechaFin = dto.FechaFin,
            Estado = estado,
        };
        _db.Seasons.Add(season);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = season.Id }, new SeasonDto
        {
            Id = season.Id,
            Nombre = season.Nombre,
            FechaInicio = season.FechaInicio,
            FechaFin = season.FechaFin,
            Estado = season.Estado.ToString(),
        });
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "ADMIN_AMBC")]
    public async Task<IActionResult> Update(Guid id, [FromBody] SeasonUpdateDto dto)
    {
        var s = await _db.Seasons.FindAsync(id);
        if (s is null) return NotFound();
        if (s.Estado == SeasonEstado.FINISHED)
            return BadRequest(new { error = "No se puede editar una temporada finalizada." });

        s.Nombre = dto.Nombre.Trim();
        s.FechaInicio = dto.FechaInicio;
        s.FechaFin = dto.FechaFin;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // Activar: UPCOMING → ACTIVE
    [HttpPost("{id}/activar")]
    [Authorize(Roles = "ADMIN_AMBC")]
    public async Task<IActionResult> Activate(Guid id)
    {
        var s = await _db.Seasons.FindAsync(id);
        if (s is null) return NotFound();
        if (s.Estado == SeasonEstado.FINISHED)
            return BadRequest(new { error = "La temporada ya finalizó." });

        // Solo una temporada ACTIVE a la vez
        var active = await _db.Seasons.AnyAsync(x => x.Estado == SeasonEstado.ACTIVE && x.Id != id);
        if (active)
            return BadRequest(new { error = "Ya existe una temporada activa. Finalízala primero." });

        s.Estado = SeasonEstado.ACTIVE;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // Finalizar: ACTIVE → FINISHED
    [HttpPost("{id}/finalizar")]
    [Authorize(Roles = "ADMIN_AMBC")]
    public async Task<IActionResult> Finish(Guid id)
    {
        var s = await _db.Seasons.FindAsync(id);
        if (s is null) return NotFound();
        if (s.Estado != SeasonEstado.ACTIVE)
            return BadRequest(new { error = "Solo se puede finalizar una temporada activa." });

        s.Estado = SeasonEstado.FINISHED;
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
