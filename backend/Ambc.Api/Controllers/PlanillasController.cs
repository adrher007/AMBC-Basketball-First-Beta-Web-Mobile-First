using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ambc.Api.Models;
using Ambc.Api.Data;
using Ambc.Api.Services;

namespace Ambc.Api.Controllers;

[ApiController]
[Route("api/planillas")]
[Authorize(Roles = "ARBITRO")]
public class PlanillasController : ControllerBase
{
    private readonly AmbcDbContext _db;
    private readonly GroqService _groq;
    private readonly IWebHostEnvironment _env;

    public PlanillasController(AmbcDbContext db, GroqService groq, IWebHostEnvironment env)
    {
        _db = db;
        _groq = groq;
        _env = env;
    }

    // ===== Partidos asignados a este árbitro (GameReferees) =====
    [HttpGet("partidos")]
    public async Task<IActionResult> PartidosDisponibles()
    {
        var referee = await GetReferee();
        if (referee is null) return NotFound(new { error = "Árbitro no registrado." });

        // Partidos donde este árbitro está asignado y:
        //  - pendientes (SCHEDULED) sin planilla, o
        //  - con planilla en captura (aún no subida).
        // Una vez subida/procesada la planilla, la tarjeta desaparece de la lista
        // operativa (el Game y la Planilla siguen en la base de datos).
        var games = await _db.Games
            .Where(g => _db.GameReferees.Any(gr => gr.GameId == g.Id && gr.RefereeId == referee.Id)
                && g.Estado == GameEstado.SCHEDULED
                && !_db.Planillas.Any(p => p.GameId == g.Id
                    && (p.Estado == PlanillaEstado.PROCESADA || p.Estado == PlanillaEstado.PROCESANDO)))
            .OrderBy(g => g.Fecha)
            .ThenBy(g => g.Hora)
            .Select(g => new
            {
                g.Id,
                g.Fecha,
                g.Hora,
                g.HomeTeamId,
                g.AwayTeamId,
                g.Fase,
                g.Estado,
            })
            .ToListAsync();

        var teams = await _db.Teams.ToDictionaryAsync(t => t.Id, t => t.NombreCorto);

        // Árbitros asignados a cada partido (para mostrar los 2-3 y marcar "Tú")
        var asignaciones = await _db.GameReferees
            .Join(_db.Referees, gr => gr.RefereeId, r => r.Id, (gr, r) => new { gr.GameId, r.Id, r.UserId })
            .Join(_db.Users, x => x.UserId, u => u.Id, (x, u) => new { x.GameId, x.Id, u.Nombre })
            .ToListAsync();

        // Estado de planilla por partido
        var planillas = await _db.Planillas
            .Select(p => new { p.GameId, p.Estado })
            .ToListAsync();

        var result = games.Select(g => new
        {
            g.Id,
            g.Fecha,
            g.Hora,
            Local = teams.GetValueOrDefault(g.HomeTeamId, "?"),
            Visitante = teams.GetValueOrDefault(g.AwayTeamId, "?"),
            g.Fase,
            Estado = g.Estado.ToString(),
            Arbitros = asignaciones
                .Where(a => a.GameId == g.Id)
                .Select(a => new { a.Id, a.Nombre, Tu = a.Id == referee.Id })
                .ToList(),
            PlanillaEstado = planillas.FirstOrDefault(p => p.GameId == g.Id)?.Estado.ToString(),
        });

        return Ok(result);
    }

    // ===== Crear planilla (captura inicial) — compartida entre los árbitros asignados =====
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] PlanillaCreateDto dto)
    {
        var referee = await GetReferee();
        if (referee is null) return NotFound(new { error = "Árbitro no registrado." });

        var game = await _db.Games.FindAsync(dto.GameId);
        if (game is null) return NotFound(new { error = "Partido no encontrado." });

        // Seguridad: el árbitro debe estar asignado al partido (GameReferees)
        if (!await EstaAsignado(dto.GameId, referee.Id))
            return StatusCode(403, new { error = "No tienes asignado este partido." });

        if (game.Estado != GameEstado.SCHEDULED)
            return BadRequest(new { error = "El partido ya está finalizado o no es elegible." });

        // Si otro árbitro asignado ya la creó, se reutiliza (no se crea otra)
        var existente = await _db.Planillas.FirstOrDefaultAsync(p => p.GameId == dto.GameId);
        if (existente is not null)
        {
            var creadaPor = await _db.Users
                .Where(u => u.Id == _db.Referees.Where(r => r.Id == existente.ArbitroId).Select(r => r.UserId).FirstOrDefault())
                .Select(u => u.Nombre)
                .FirstOrDefaultAsync();

            return Ok(new PlanillaDto
            {
                Id = existente.Id,
                GameId = existente.GameId,
                Estado = existente.Estado.ToString(),
                ImagenUrl = existente.ImagenUrl,
                DatosExtraidosJson = existente.DatosExtraidosJson,
                DatosCorregidosJson = existente.DatosCorregidosJson,
                CreadaPor = creadaPor,
                Compartida = true,
            });
        }

        var planilla = new Planilla
        {
            GameId = dto.GameId,
            ArbitroId = referee.Id,
            Estado = PlanillaEstado.CAPTURADA,
        };
        _db.Planillas.Add(planilla);
        await _db.SaveChangesAsync();

        return Ok(new PlanillaDto
        {
            Id = planilla.Id,
            GameId = planilla.GameId,
            Estado = "CAPTURADA",
            Compartida = false,
        });
    }

    // ===== Subir imagen + procesar (resize/contraste + Groq) =====
    [HttpPost("{id}/imagen")]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10 MB
    public async Task<IActionResult> SubirImagen(Guid id, IFormFile file)
    {
        var planilla = await _db.Planillas.FindAsync(id);
        if (planilla is null) return NotFound();
        if (planilla.Estado == PlanillaEstado.CONFIRMADA)
            return BadRequest(new { error = "La planilla ya está confirmada (inmutable)." });

        // Seguridad: solo árbitros asignados al partido pueden modificarla
        var refImg = await GetReferee();
        if (refImg is null || !await EstaAsignado(planilla.GameId, refImg.Id))
            return StatusCode(403, new { error = "No tienes asignado este partido." });

        if (file is null || file.Length == 0)
            return BadRequest(new { error = "Imagen requerida." });

        // Procesar imagen
        byte[] processed;
        using (var ms = new MemoryStream())
        {
            await file.CopyToAsync(ms);
            try
            {
                processed = PlanillaImageProcessor.Procesar(ms.ToArray());
            }
            catch (Exception)
            {
                return BadRequest(new { error = "Imagen inválida o corrupta." });
            }
        }

        // Guardar imagen
        var uploads = Path.Combine(_env.ContentRootPath, "uploads", "planillas");
        Directory.CreateDirectory(uploads);
        var filename = $"{planilla.Id}.jpg";
        await System.IO.File.WriteAllBytesAsync(Path.Combine(uploads, filename), processed);
        planilla.ImagenUrl = $"/uploads/planillas/{filename}";

        // Estado: PROCESANDO
        planilla.Estado = PlanillaEstado.PROCESANDO;
        await _db.SaveChangesAsync();

        // Intentar Groq (si hay key configurada)
        var dto = new PlanillaDto { Id = planilla.Id, GameId = planilla.GameId, ImagenUrl = planilla.ImagenUrl };

        try
        {
            var base64 = Convert.ToBase64String(processed);
            // Modo completo si existe la imagen guía de referencia (formato oficial AMBC)
            var guiaPath = Path.Combine(_env.ContentRootPath, "Resources", "planilla_guia.jpg");
            var guiaBase64 = System.IO.File.Exists(guiaPath)
                ? Convert.ToBase64String(await System.IO.File.ReadAllBytesAsync(guiaPath))
                : null;
            var modoCompleto = guiaBase64 is not null;

            var json = await _groq.ExtraerPlanilla(base64, modoCompleto, guiaBase64);
            planilla.DatosExtraidosJson = json;
            planilla.Estado = PlanillaEstado.PROCESADA;
            await _db.SaveChangesAsync();

            dto.Estado = "PROCESADA";
            dto.DatosExtraidosJson = json;
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("API key"))
        {
            // Modo degradado: sin key de Groq
            planilla.Estado = PlanillaEstado.PROCESADA;
            await _db.SaveChangesAsync();
            dto.Estado = "PROCESADA";
            dto.Error = "API key de Groq no configurada. Revisa la imagen manualmente.";
        }
        catch (Exception)
        {
            // Groq falló: permitir revisión manual
            planilla.Estado = PlanillaEstado.PROCESADA;
            await _db.SaveChangesAsync();
            dto.Estado = "PROCESADA";
            dto.Error = "El procesamiento automático falló. Revisa la imagen manualmente.";
        }

        return Ok(dto);
    }

    // ===== Obtener planilla (para revisión) =====
    [HttpGet("{id}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var p = await _db.Planillas.FindAsync(id);
        if (p is null) return NotFound();

        // Seguridad: solo árbitros asignados al partido
        var refGet = await GetReferee();
        if (refGet is null || !await EstaAsignado(p.GameId, refGet.Id))
            return StatusCode(403, new { error = "No tienes asignado este partido." });

        var game = await _db.Games.FindAsync(p.GameId);
        return Ok(new PlanillaDto
        {
            Id = p.Id,
            GameId = p.GameId,
            Estado = p.Estado.ToString(),
            ImagenUrl = p.ImagenUrl,
            DatosExtraidosJson = p.DatosExtraidosJson,
            DatosCorregidosJson = p.DatosCorregidosJson,
            MarcadorLocal = game?.MarcadorLocal.ToString(),
            MarcadorVisitante = game?.MarcadorVisitante.ToString(),
        });
    }

    // ===== Confirmar planilla (marcador + datos corregidos) → inmutable =====
    [HttpPost("{id}/confirmar")]
    public async Task<IActionResult> Confirmar(Guid id, [FromBody] ConfirmarPlanillaDto dto)
    {
        var planilla = await _db.Planillas.FindAsync(id);
        if (planilla is null) return NotFound();
        if (planilla.Estado == PlanillaEstado.CONFIRMADA)
            return BadRequest(new { error = "La planilla ya está confirmada (inmutable)." });

        // Seguridad: solo árbitros asignados al partido
        var refConf = await GetReferee();
        if (refConf is null || !await EstaAsignado(planilla.GameId, refConf.Id))
            return StatusCode(403, new { error = "No tienes asignado este partido." });

        if (dto.MarcadorLocal == dto.MarcadorVisitante)
            return BadRequest(new { error = "No se permiten empates (prórroga)." });

        var game = await _db.Games.FindAsync(planilla.GameId);
        if (game is null) return NotFound();
        if (game.Estado == GameEstado.FINISHED)
            return BadRequest(new { error = "El partido ya tiene resultado." });

        var userId = GetUserId();

        // 1.5 — La suma de puntos por equipo debe coincidir con el marcador declarado
        if (!string.IsNullOrWhiteSpace(dto.DatosCorregidosJson))
        {
            var suma = StatsMapper.SumarPuntos(dto.DatosCorregidosJson);
            if (suma is not null)
            {
                if (suma.Value.Local != dto.MarcadorLocal)
                    return BadRequest(new { error = $"Suma de puntos local ({suma.Value.Local}) ≠ marcador ({dto.MarcadorLocal})." });
                if (suma.Value.Visitante != dto.MarcadorVisitante)
                    return BadRequest(new { error = $"Suma de puntos visitante ({suma.Value.Visitante}) ≠ marcador ({dto.MarcadorVisitante})." });
            }
        }

        // 1.2 — Opción A: transacción atómica, si playoffs falla se cancela la confirmación entera.
        // Todo (stats + planilla CONFIRMADA + partido FINISHED + standings/playoffs) es un
        // solo commit: o queda todo consistente, o no queda nada.
        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            // ===== Escribir stats (si vienen datos corregidos) =====
            if (!string.IsNullOrWhiteSpace(dto.DatosCorregidosJson))
            {
                var (_, err) = await StatsMapper.EscribirStats(
                    _db, game.SeasonId, game.Id, game.HomeTeamId, game.AwayTeamId, dto.DatosCorregidosJson);
                if (!string.IsNullOrEmpty(err))
                {
                    await tx.RollbackAsync();
                    return BadRequest(new { error = err });
                }
            }

            // Marcar confirmada (inmutable)
            planilla.Estado = PlanillaEstado.CONFIRMADA;
            planilla.DatosCorregidosJson = dto.DatosCorregidosJson;
            planilla.ConfirmadaPor = userId;
            await _db.SaveChangesAsync();

            // Registrar resultado en el partido
            game.MarcadorLocal = dto.MarcadorLocal;
            game.MarcadorVisitante = dto.MarcadorVisitante;
            game.GanadorId = dto.MarcadorLocal > dto.MarcadorVisitante ? game.HomeTeamId : game.AwayTeamId;
            game.Estado = GameEstado.FINISHED;
            await _db.SaveChangesAsync();

            // Recalcular standings (fase regular) o avanzar playoffs — dentro de la misma tx
            if (game.PlayoffSeriesId is null)
                await StandingsCalculator.Recalcular(_db, game.SeasonId);
            else
                await PlayoffService.TrasResultado(_db, game.SeasonId, game.PlayoffSeriesId.Value);

            await tx.CommitAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            // 1.3 — Otra sesión modificó la planilla o el partido mientras se confirmaba
            await tx.RollbackAsync();
            return Conflict(new { error = "La planilla o el partido fueron modificados por otra sesión. Recarga e intenta de nuevo." });
        }
        catch (System.ComponentModel.DataAnnotations.ValidationException ex)
        {
            // 1.4 — Pts inconsistente con sus componentes → 400
            await tx.RollbackAsync();
            return BadRequest(new { error = ex.Message });
        }
        catch
        {
            // Cualquier fallo posterior (p. ej. playoffs) revierte TODO: la planilla no
            // queda confirmada ni el partido FINISHED.
            await tx.RollbackAsync();
            throw;
        }

        return Ok(new
        {
            mensaje = "Planilla confirmada. Resultado registrado.",
            Estado = game.Estado.ToString(),
            game.MarcadorLocal,
            game.MarcadorVisitante,
            game.GanadorId,
        });
    }

    // ===== Helpers =====

    /// <summary>¿El árbitro está asignado a este partido? (tabla GameReferees)</summary>
    private Task<bool> EstaAsignado(Guid gameId, Guid refereeId) =>
        _db.GameReferees.AnyAsync(gr => gr.GameId == gameId && gr.RefereeId == refereeId);

    private async Task<Referee?> GetReferee()
    {
        var userId = GetUserId();
        if (userId is null) return null;
        return await _db.Referees.FirstOrDefaultAsync(r => r.UserId == userId.Value);
    }

    private Guid? GetUserId()
    {
        var sub = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(sub, out var id) ? id : null;
    }
}

public class PlanillaCreateDto
{
    public Guid GameId { get; set; }
}
