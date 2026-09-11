namespace Ambc.Api.Services;

/// <summary>
/// Genera el fixture round-robin doble (ida y vuelta) con el "circle method".
/// Con N impar se agrega un dummy (bye): cada jornada un equipo descansa.
/// </summary>
public static class FixtureGenerator
{
    /// <summary>
    /// Devuelve las rondas. Cada ronda es una lista de pares (home, away).
    /// El dummy (bye) se omite, por lo que con 9 equipos cada ronda tiene 4 partidos.
    /// </summary>
    public static List<List<(Guid Home, Guid Away)>> GenerateRoundRobin(
        IReadOnlyList<Guid> teams,
        bool isReturnLeg)
    {
        var n = teams.Count;
        var isOdd = n % 2 != 0;
        var size = isOdd ? n + 1 : n;

        // Construir el array con participants (dummy = null si impar)
        var arr = new List<Guid?>();
        foreach (var t in teams) arr.Add(t);
        if (isOdd) arr.Add(null);

        var rounds = new List<List<(Guid, Guid)>>();
        var teamList = new List<Guid?>(arr);

        for (var round = 0; round < size - 1; round++)
        {
            var roundPairs = new List<(Guid, Guid)>();

            // Emparejar: i con (size-1-i). El dummy produce bye (se omite).
            for (var i = 0; i < size / 2; i++)
            {
                var a = teamList[i];
                var b = teamList[size - 1 - i];
                if (a is null || b is null) continue;

                // Balance de localías: en la IDA se alterna por ronda.
                // En la VUELTA se invierte siempre respecto de la ida → ida/vuelta REAL
                // (reglamento AMBC). El flip de la vuelta debe ser el complemento del de
                // la ida; antes era el mismo valor, así que la vuelta repetía la ida.
                var flip = isReturnLeg ? (round % 2 == 0) : (round % 2 == 1);
                if (flip)
                    roundPairs.Add((b.Value, a.Value));
                else
                    roundPairs.Add((a.Value, b.Value));
            }

            rounds.Add(roundPairs);

            // Rotación del circle method: fijo el primero, roto el resto una posición.
            var first = teamList[0];
            var rest = teamList.GetRange(1, teamList.Count - 1);
            var last = rest[rest.Count - 1];
            rest.RemoveAt(rest.Count - 1);
            rest.Insert(0, last);

            teamList = new List<Guid?> { first };
            teamList.AddRange(rest);
        }

        return rounds;
    }
}
