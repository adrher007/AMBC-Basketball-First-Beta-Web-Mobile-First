using System.ComponentModel.DataAnnotations;
using Ambc.Api.Data;
using Ambc.Api.Models;
using Ambc.Api.Services;

namespace Ambc.Api.Tests;

/// <summary>FixtureGenerator, SemanaProgramacion.ValidarSemana y StatsMapper.</summary>
public class FixtureSemanaStatsTests
{
    // ================= FixtureGenerator =================

    [Fact]
    public void Round_robin_de_9_equipos_da_72_partidos_en_18_rondas()
    {
        var equipos = Enumerable.Range(1, 9).Select(_ => Guid.NewGuid()).ToList();

        var ida = FixtureGenerator.GenerateRoundRobin(equipos, isReturnLeg: false);
        var vuelta = FixtureGenerator.GenerateRoundRobin(equipos, isReturnLeg: true);

        Assert.Equal(9, ida.Count);
        Assert.Equal(9, vuelta.Count);
        Assert.Equal(72, ida.Sum(r => r.Count) + vuelta.Sum(r => r.Count));

        // 9 equipos (impar) → 4 partidos y 1 equipo libre por ronda
        Assert.All(ida, ronda => Assert.Equal(4, ronda.Count));

        // Cada ronda usa 8 equipos distintos (nadie juega dos veces en la misma jornada)
        Assert.All(ida, ronda =>
        {
            var equiposRonda = ronda.SelectMany(p => new[] { p.Home, p.Away }).ToList();
            Assert.Equal(8, equiposRonda.Distinct().Count());
        });

        // Ida: cada pareja aparece una sola vez
        var paresIda = ida.SelectMany(r => r).Select(p => (p.Home, p.Away)).ToList();
        Assert.Equal(paresIda.Count, paresIda.Distinct().Count());
    }

    /// <summary>
    /// HALLAZGO (bug preexistente): GenerateRoundRobin(isReturnLeg: true) NO invierte
    /// la localía respecto de la ida. En FixtureGenerator.cs:41 el flip de la vuelta
    /// es el complementario del de la ida, así que la vuelta repite exactamente los
    /// mismos pares (Home, Away): no hay ida/vuelta real.
    /// Deshabilitado a la espera de decisión (la spec del fixture pide no tocarlo).
    /// Fix propuesto (1 línea): en la vuelta invertir siempre el par → flip = isReturnLeg.
    /// </summary>
    [Fact(Skip = "Bug preexistente: la vuelta repite la ida sin invertir localía")]
    public void Vuelta_invierte_la_localia_respecto_de_la_ida()
    {
        var equipos = Enumerable.Range(1, 9).Select(_ => Guid.NewGuid()).ToList();

        var ida = FixtureGenerator.GenerateRoundRobin(equipos, isReturnLeg: false);
        var vuelta = FixtureGenerator.GenerateRoundRobin(equipos, isReturnLeg: true);

        var paresIda = ida.SelectMany(r => r).Select(p => (p.Home, p.Away)).ToHashSet();
        var paresVuelta = vuelta.SelectMany(r => r).Select(p => (p.Home, p.Away)).ToHashSet();

        // La vuelta debe contener las parejas invertidas
        Assert.All(paresIda, p => Assert.Contains((p.Away, p.Home), paresVuelta));
    }

    [Fact]
    public void Round_robin_con_cantidad_par_de_equipos_no_deja_libre()
    {
        var equipos = Enumerable.Range(1, 10).Select(_ => Guid.NewGuid()).ToList();
        var ida = FixtureGenerator.GenerateRoundRobin(equipos, isReturnLeg: false);

        Assert.All(ida, ronda => Assert.Equal(5, ronda.Count));
        Assert.All(ida, ronda =>
        {
            var equiposRonda = ronda.SelectMany(p => new[] { p.Home, p.Away }).ToList();
            Assert.Equal(10, equiposRonda.Distinct().Count());
        });
    }

    // ================= SemanaProgramacion.ValidarSemana =================

    private static List<SemanaPartidoDto> Partidos(AmbcDbContext db, Season s,
        List<Referee> arbitros, params (Team home, Team away, DateOnly fecha)[] juegos) =>
        juegos.Select(j => new SemanaPartidoDto
        {
            Fecha = j.fecha,
            Hora = "19:00",
            HomeTeamId = j.home.Id,
            AwayTeamId = j.away.Id,
            ArbitroIds = arbitros.Select(a => a.Id).ToList(),
        }).ToList();

    [Fact]
    public async Task ValidarSemana_rechaza_local_igual_a_visitante()
    {
        var db = TestDb.Crear();
        var s = TestDb.Season(db, new DateOnly(2026, 9, 7), new DateOnly(2026, 12, 31));
        var a = TestDb.Team(db, "A", "AAA");
        var r1 = TestDb.Referee(db, "R1");
        var r2 = TestDb.Referee(db, "R2");
        var r3 = TestDb.Referee(db, "R3");

        var partidos = Partidos(db, s, new List<Referee> { r1, r2, r3 },
            (a, a, new DateOnly(2026, 9, 8)));

        var error = await SemanaProgramacion.ValidarSemana(db, s.Id, 1, partidos);
        Assert.Equal("El equipo local y el visitante deben ser diferentes.", error);
    }

    [Fact]
    public async Task ValidarSemana_rechaza_equipo_dos_veces_el_mismo_dia()
    {
        var db = TestDb.Crear();
        var s = TestDb.Season(db, new DateOnly(2026, 9, 7), new DateOnly(2026, 12, 31));
        var a = TestDb.Team(db, "A", "AAA");
        var b = TestDb.Team(db, "B", "BBB");
        var c = TestDb.Team(db, "C", "CCC");
        var r1 = TestDb.Referee(db, "R1");
        var r2 = TestDb.Referee(db, "R2");
        var r3 = TestDb.Referee(db, "R3");

        var fecha = new DateOnly(2026, 9, 8);
        var partidos = Partidos(db, s, new List<Referee> { r1, r2, r3 },
            (a, b, fecha),
            (c, a, fecha));   // A juega dos veces ese día

        var error = await SemanaProgramacion.ValidarSemana(db, s.Id, 1, partidos);
        Assert.Equal($"Un equipo no puede jugar dos veces el mismo día ({fecha:dd/MM/yyyy}).", error);
    }

    [Fact]
    public async Task ValidarSemana_rechaza_menos_de_dos_arbitros()
    {
        var db = TestDb.Crear();
        var s = TestDb.Season(db, new DateOnly(2026, 9, 7), new DateOnly(2026, 12, 31));
        var a = TestDb.Team(db, "A", "AAA");
        var b = TestDb.Team(db, "B", "BBB");
        var r1 = TestDb.Referee(db, "R1");

        var partidos = new List<SemanaPartidoDto>
        {
            new()
            {
                Fecha = new DateOnly(2026, 9, 8), Hora = "19:00",
                HomeTeamId = a.Id, AwayTeamId = b.Id,
                ArbitroIds = new List<Guid> { r1.Id },
            },
        };

        var error = await SemanaProgramacion.ValidarSemana(db, s.Id, 1, partidos);
        Assert.Equal("Cada partido requiere al menos 2 árbitros.", error);
    }

    [Fact]
    public async Task ValidarSemana_acepta_una_jornada_valida()
    {
        var db = TestDb.Crear();
        var s = TestDb.Season(db, new DateOnly(2026, 9, 7), new DateOnly(2026, 12, 31));
        var a = TestDb.Team(db, "A", "AAA");
        var b = TestDb.Team(db, "B", "BBB");
        var r1 = TestDb.Referee(db, "R1");
        var r2 = TestDb.Referee(db, "R2");

        var partidos = Partidos(db, s, new List<Referee> { r1, r2 },
            (a, b, new DateOnly(2026, 9, 8)));

        var error = await SemanaProgramacion.ValidarSemana(db, s.Id, 1, partidos);
        Assert.Null(error);
    }

    // ================= StatsMapper =================

    [Fact]
    public async Task StatsMapper_rechaza_Pts_inconsistente_con_sus_componentes()
    {
        var db = TestDb.Crear();
        var s = TestDb.Season(db, new DateOnly(2026, 9, 7), new DateOnly(2026, 12, 31));
        var a = TestDb.Team(db, "A", "AAA");
        var b = TestDb.Team(db, "B", "BBB");
        var juego = TestDb.Game(db, s, a, b, new DateOnly(2026, 9, 8));
        TestDb.Roster(db, s, a, numero: 5);

        // pts=10 pero dos_p=3 → 3×2 = 6 ≠ 10
        var json = """
        {"schemaVersion":1,"jugadores_local":[{"numero":5,"nombre":"N","apellido":"A","pts":10,"dos_p":3}],
         "jugadores_visitante":[]}
        """;

        await Assert.ThrowsAsync<ValidationException>(() =>
            StatsMapper.EscribirStats(db, s.Id, juego.Id, a.Id, b.Id, json));
    }

    [Fact]
    public async Task StatsMapper_rechaza_numero_que_no_esta_en_el_roster()
    {
        var db = TestDb.Crear();
        var s = TestDb.Season(db, new DateOnly(2026, 9, 7), new DateOnly(2026, 12, 31));
        var a = TestDb.Team(db, "A", "AAA");
        var b = TestDb.Team(db, "B", "BBB");
        var juego = TestDb.Game(db, s, a, b, new DateOnly(2026, 9, 8));
        TestDb.Roster(db, s, a, numero: 5);

        var json = """
        {"schemaVersion":1,"jugadores_local":[{"numero":99,"nombre":"N","apellido":"A","pts":10}],
         "jugadores_visitante":[]}
        """;

        var (escritos, error) = await StatsMapper.EscribirStats(db, s.Id, juego.Id, a.Id, b.Id, json);

        Assert.Equal(0, escritos);
        Assert.Contains("no está en el roster", error);
    }

    [Fact]
    public async Task StatsMapper_rechaza_schemaVersion_no_soportada()
    {
        var db = TestDb.Crear();
        var s = TestDb.Season(db, new DateOnly(2026, 9, 7), new DateOnly(2026, 12, 31));
        var a = TestDb.Team(db, "A", "AAA");
        var b = TestDb.Team(db, "B", "BBB");
        var juego = TestDb.Game(db, s, a, b, new DateOnly(2026, 9, 8));

        var json = """
        {"schemaVersion":2,"jugadores_local":[],"jugadores_visitante":[]}
        """;

        var (escritos, error) = await StatsMapper.EscribirStats(db, s.Id, juego.Id, a.Id, b.Id, json);

        Assert.Equal(0, escritos);
        Assert.Contains("schemaVersion 2 no soportada", error);
    }

    [Fact]
    public async Task StatsMapper_escribe_stats_con_Pts_correcto()
    {
        var db = TestDb.Crear();
        var s = TestDb.Season(db, new DateOnly(2026, 9, 7), new DateOnly(2026, 12, 31));
        var a = TestDb.Team(db, "A", "AAA");
        var b = TestDb.Team(db, "B", "BBB");
        var juego = TestDb.Game(db, s, a, b, new DateOnly(2026, 9, 8));
        TestDb.Roster(db, s, a, numero: 5);
        TestDb.Roster(db, s, b, numero: 7);

        var json = """
        {"schemaVersion":1,
         "jugadores_local":[{"numero":5,"nombre":"N","apellido":"A","pts":22}],
         "jugadores_visitante":[{"numero":7,"nombre":"M","apellido":"B","pts":18}]}
        """;

        var (escritos, error) = await StatsMapper.EscribirStats(db, s.Id, juego.Id, a.Id, b.Id, json);

        Assert.Equal(2, escritos);
        Assert.Empty(error);

        var stats = db.PlayerGameStats.Where(x => x.GameId == juego.Id).ToList();
        Assert.Equal(22, stats.Single(x => x.TeamId == a.Id).Pts);
        Assert.Equal(18, stats.Single(x => x.TeamId == b.Id).Pts);
    }

    [Fact]
    public void SumarPuntos_totaliza_por_equipo()
    {
        var json = """
        {"schemaVersion":1,
         "jugadores_local":[{"numero":5,"nombre":"N","apellido":"A","pts":22},{"numero":6,"nombre":"O","apellido":"B","pts":8}],
         "jugadores_visitante":[{"numero":7,"nombre":"M","apellido":"C","pts":18}]}
        """;

        var suma = StatsMapper.SumarPuntos(json);

        Assert.NotNull(suma);
        Assert.Equal(30, suma!.Value.Local);
        Assert.Equal(18, suma.Value.Visitante);
    }
}
