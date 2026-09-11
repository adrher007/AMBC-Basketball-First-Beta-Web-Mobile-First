using Ambc.Api.Data;
using Ambc.Api.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Ambc.Api.Tests;

/// <summary>
/// Contexto de test: SQLite in-memory.
/// En PostgreSQL RowVersion la genera un trigger + DEFAULT; SQLite no tiene ese
/// mecanismo, así que aquí se marca como ValueGeneratedNever para que EF envíe
/// el valor en el INSERT. El byte[] de producción no se modifica.
/// Los tipos jsonb se declaran igual (SQLite usa afinidad dinámica → TEXT).
/// </summary>
public class TestAmbcDbContext : AmbcDbContext
{
    public TestAmbcDbContext(DbContextOptions options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder m)
    {
        base.OnModelCreating(m);

        m.Entity<Game>().Property(g => g.RowVersion).ValueGeneratedNever();
        m.Entity<Planilla>().Property(p => p.RowVersion).ValueGeneratedNever();
    }
}

public static class TestDb
{
    public static AmbcDbContext Crear()
    {
        var conn = new SqliteConnection("Filename=:memory:");
        conn.Open();
        var opts = new DbContextOptionsBuilder<AmbcDbContext>()
            .UseSqlite(conn)
            .Options;
        var db = new TestAmbcDbContext(opts);
        db.Database.EnsureCreated();
        return db;
    }

    // ===== Builders mínimos =====

    public static Season Season(AmbcDbContext db, DateOnly inicio, DateOnly fin,
        SeasonEstado estado = SeasonEstado.ACTIVE, string nombre = "Test")
    {
        var s = new Season { Nombre = nombre, FechaInicio = inicio, FechaFin = fin, Estado = estado };
        db.Seasons.Add(s);
        db.SaveChanges();
        return s;
    }

    public static Team Team(AmbcDbContext db, string nombre, string corto)
    {
        var t = new Team { Nombre = nombre, NombreCorto = corto };
        db.Teams.Add(t);
        db.SaveChanges();
        return t;
    }

    public static Game Game(AmbcDbContext db, Season season, Team home, Team away,
        DateOnly fecha, int ml = 0, int mv = 0, GameEstado estado = GameEstado.SCHEDULED,
        Guid? playoffSeriesId = null)
    {
        var g = new Game
        {
            SeasonId = season.Id,
            Fase = playoffSeriesId is null ? "REGULAR" : "PLAYOFF",
            HomeTeamId = home.Id,
            AwayTeamId = away.Id,
            Fecha = fecha,
            Hora = "19:00",
            Estado = estado,
            MarcadorLocal = ml,
            MarcadorVisitante = mv,
            GanadorId = estado == GameEstado.FINISHED ? (ml > mv ? home.Id : away.Id) : null,
            PlayoffSeriesId = playoffSeriesId,
            Semana = 1,
        };
        db.Games.Add(g);
        db.SaveChanges();
        return g;
    }

    public static Roster Roster(AmbcDbContext db, Season season, Team team, int numero,
        string nombre = "N", string apellido = "A")
    {
        var p = new Player { Nombre = nombre, Apellido = apellido, Posicion = "BASE" };
        db.Players.Add(p);
        db.SaveChanges();

        var r = new Roster
        {
            SeasonId = season.Id,
            TeamId = team.Id,
            PlayerId = p.Id,
            Numero = numero,
            Activo = true,
        };
        db.Rosters.Add(r);
        db.SaveChanges();
        return r;
    }

    public static Referee Referee(AmbcDbContext db, string nombre)
    {
        var u = new User
        {
            Nombre = nombre,
            Username = nombre.ToLowerInvariant().Replace(" ", ""),
            PasswordHash = "x",
            Rol = Rol.ARBITRO,
            Activo = true,
        };
        db.Users.Add(u);
        db.SaveChanges();

        var r = new Referee { UserId = u.Id, CreadoPor = u.Id };
        db.Referees.Add(r);
        db.SaveChanges();
        return r;
    }
}
