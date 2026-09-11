using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Ambc.Api.Models;

// ===== Enums =====
public enum Rol { ADMIN_AMBC, COACH, ARBITRO, FAN }

public enum SeasonEstado { UPCOMING, ACTIVE, FINISHED }

public enum FaseSerie { QF, SF, FINAL, TERCER_LUGAR }

public enum GameEstado { SCHEDULED, FINISHED }

public enum PlanillaEstado { CAPTURADA, PROCESANDO, PROCESADA, CONFIRMADA }

public enum DestinatarioTipo { ALL, COACH, FAN, ARBITRO, TEAM_X, USER_X }

public enum Posicion { BASE, ESCOLTA, ALERO, ALA_PIVOT, PIVOT }

// ===== Entidades =====
public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Nombre { get; set; } = "";        // nombre completo (también username para COACH/ARBITRO)
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public Rol Rol { get; set; }
    public bool Activo { get; set; } = true;
}

public class Season
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Nombre { get; set; } = "";
    public DateOnly FechaInicio { get; set; }
    public DateOnly FechaFin { get; set; }
    public SeasonEstado Estado { get; set; } = SeasonEstado.UPCOMING;
}

public class Team
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Nombre { get; set; } = "";
    public string NombreCorto { get; set; } = "";
    public string? LogoUrl { get; set; }
    public Guid? LogoActualizadoPor { get; set; }   // user_id
}

public class Player
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Nombre { get; set; } = "";
    public string Apellido { get; set; } = "";
    public string Posicion { get; set; } = "";  // enum Posicion como string
}

public class Roster
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SeasonId { get; set; }
    public Guid TeamId { get; set; }
    public Guid PlayerId { get; set; }
    public int Numero { get; set; }
    public bool Activo { get; set; } = true;
}

public class Coach
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid TeamId { get; set; }
    public Guid SeasonId { get; set; }
}

public class Referee
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid CreadoPor { get; set; }             // admin user_id
}

/// <summary>
/// Tabla intermedia partido ↔ árbitro (2..3 árbitros por partido).
/// No se agrega arbitro_id a Games.
/// </summary>
public class GameReferee
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GameId { get; set; }
    public Guid RefereeId { get; set; }
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    public Guid AssignedBy { get; set; }            // admin user_id
}

public class PlayoffSeries
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SeasonId { get; set; }
    public FaseSerie Fase { get; set; }
    public Guid TeamAId { get; set; }
    public Guid TeamBId { get; set; }
    public int WinsA { get; set; } = 0;
    public int WinsB { get; set; } = 0;
    public int BestOf { get; set; } = 3;
    public Guid? GanadorId { get; set; }
    public string Estado { get; set; } = "";
}

public class Game
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SeasonId { get; set; }
    public string Fase { get; set; } = "";
    public Guid? PlayoffSeriesId { get; set; }
    public Guid HomeTeamId { get; set; }
    public Guid AwayTeamId { get; set; }
    public DateOnly Fecha { get; set; }
    public string Hora { get; set; } = "";          // "HH:mm"
    public GameEstado Estado { get; set; } = GameEstado.SCHEDULED;
    public int MarcadorLocal { get; set; }
    public int MarcadorVisitante { get; set; }
    public Guid? GanadorId { get; set; }
    public int Semana { get; set; }                 // semana de planificación (1..N desde FechaInicio)
}

/// <summary>
/// Registro idempotente de eventos notificados.
/// UNIQUE(game_id, tipo, fecha) evita notificaciones duplicadas ante
/// reinicios del servidor o ejecuciones repetidas del proceso.
/// </summary>
public class NotificationEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GameId { get; set; }
    public string Tipo { get; set; } = "";          // PARTIDO_HOY | ASIGNACION
    public DateOnly Fecha { get; set; }             // fecha del Game sobre la que se notificó
    public Guid? NotificationId { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
}

public class Planilla
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GameId { get; set; }
    public Guid ArbitroId { get; set; }
    public string? ImagenUrl { get; set; }
    public PlanillaEstado Estado { get; set; } = PlanillaEstado.CAPTURADA;
    public string? DatosExtraidosJson { get; set; }  // JSONB
    public string? DatosCorregidosJson { get; set; } // JSONB
    public Guid? ConfirmadaPor { get; set; }
}

public class PlayerGameStats
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GameId { get; set; }
    public Guid PlayerId { get; set; }
    public Guid TeamId { get; set; }
    public int Pts { get; set; }
    public int DosP { get; set; }
    public int TresP { get; set; }
    public int Ft { get; set; }
    public int Reb { get; set; }
    public int Ast { get; set; }
    public int Stl { get; set; }
    public int Blk { get; set; }
    public int To { get; set; }
}

public class Standing
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SeasonId { get; set; }
    public Guid TeamId { get; set; }
    public int Pj { get; set; }
    public int Victorias { get; set; }
    public int Derrotas { get; set; }
    public int Pf { get; set; }
    public int Pc { get; set; }
    public int Diff { get; set; }
    public int Posicion { get; set; }
}

public class Notification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Mensaje { get; set; } = "";
    public string Destinatarios { get; set; } = ""; // "ALL" | "COACH" | "FAN" | "ARBITRO" | "TEAM:<id>" | "USER:<id>"
    public Guid CreadoPor { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
}

public class NotificationView
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid NotificationId { get; set; }
    public Guid UserId { get; set; }
    public DateTime VistoEn { get; set; }
}
