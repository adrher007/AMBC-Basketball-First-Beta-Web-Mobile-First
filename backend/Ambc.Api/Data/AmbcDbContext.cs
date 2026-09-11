using Microsoft.EntityFrameworkCore;
using Ambc.Api.Models;
using Npgsql;

namespace Ambc.Api.Data;

public class AmbcDbContext : DbContext
{
    public AmbcDbContext(DbContextOptions<AmbcDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Season> Seasons => Set<Season>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<Player> Players => Set<Player>();
    public DbSet<Roster> Rosters => Set<Roster>();
    public DbSet<Coach> Coaches => Set<Coach>();
    public DbSet<Referee> Referees => Set<Referee>();
    public DbSet<GameReferee> GameReferees => Set<GameReferee>();
    public DbSet<NotificationEvent> NotificationEvents => Set<NotificationEvent>();
    public DbSet<GroqUsage> GroqUsages => Set<GroqUsage>();
    public DbSet<PlayoffSeries> PlayoffSeries => Set<PlayoffSeries>();
    public DbSet<Game> Games => Set<Game>();
    public DbSet<Planilla> Planillas => Set<Planilla>();
    public DbSet<PlayerGameStats> PlayerGameStats => Set<PlayerGameStats>();
    public DbSet<Standing> Standings => Set<Standing>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationView> NotificationViews => Set<NotificationView>();

    protected override void OnModelCreating(ModelBuilder m)
    {
        base.OnModelCreating(m);

        // ===== Uniques / índices clave =====
        m.Entity<User>()
            .HasIndex(u => u.Username).IsUnique();

        m.Entity<Roster>()
            .HasIndex(r => new { r.SeasonId, r.PlayerId }).IsUnique();

        m.Entity<Coach>()
            .HasIndex(c => new { c.SeasonId, c.TeamId }).IsUnique();
        m.Entity<Coach>()
            .HasIndex(c => c.UserId).IsUnique();

        m.Entity<Referee>()
            .HasIndex(r => r.UserId).IsUnique();

        // Un árbitro no puede asignarse dos veces al mismo partido
        m.Entity<GameReferee>()
            .HasIndex(gr => new { gr.GameId, gr.RefereeId }).IsUnique();

        // Idempotencia de notificaciones: game + tipo de evento + fecha + árbitro
        m.Entity<NotificationEvent>()
            .HasIndex(ne => new { ne.GameId, ne.Tipo, ne.Fecha, ne.RefereeId }).IsUnique();

        // 3.3 — Consumo diario de Groq por usuario
        m.Entity<GroqUsage>()
            .HasIndex(g => new { g.UserId, g.Fecha }).IsUnique();

        m.Entity<Planilla>()
            .HasIndex(p => p.GameId).IsUnique();

        m.Entity<PlayerGameStats>()
            .HasIndex(s => new { s.GameId, s.PlayerId }).IsUnique();

        m.Entity<Standing>()
            .HasIndex(s => new { s.SeasonId, s.TeamId }).IsUnique();

        m.Entity<NotificationView>()
            .HasIndex(v => new { v.NotificationId, v.UserId }).IsUnique();

        // ===== Enums como texto (legibilidad + robustez ante cambios) =====
        m.Entity<User>().Property(x => x.Rol).HasConversion<string>();
        m.Entity<Season>().Property(x => x.Estado).HasConversion<string>();
        m.Entity<Game>().Property(x => x.Estado).HasConversion<string>();
        m.Entity<Planilla>().Property(x => x.Estado).HasConversion<string>();
        m.Entity<PlayoffSeries>().Property(x => x.Fase).HasConversion<string>();

        // ===== JSONB (PostgreSQL) =====
        m.Entity<Planilla>()
            .Property(p => p.DatosExtraidosJson).HasColumnType("jsonb");
        m.Entity<Planilla>()
            .Property(p => p.DatosCorregidosJson).HasColumnType("jsonb");
    }
}
