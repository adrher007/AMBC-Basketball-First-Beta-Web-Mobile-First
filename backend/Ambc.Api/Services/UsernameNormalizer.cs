using System.Text;

namespace Ambc.Api.Services;

/// <summary>
/// Normaliza un nombre completo a username de login:
/// minúsculas, sin tildes, sin espacios.
/// </summary>
public static class UsernameNormalizer
{
    public static string Normalize(string nombreCompleto)
    {
        if (string.IsNullOrWhiteSpace(nombreCompleto)) return "";
        var s = nombreCompleto.Trim().ToLowerInvariant();
        s = s.Replace(" ", "")
             .Replace("á", "a").Replace("é", "e").Replace("í", "i")
             .Replace("ó", "o").Replace("ú", "u")
             .Replace("ñ", "n")
             .Replace("ü", "u");
        return s;
    }

    /// <summary>
    /// 3.2 — Devuelve un username libre a partir del base, sufijando 2, 3, 4...
    /// (juanperez → juanperez2 → juanperez3) hasta un máximo de intentos.
    /// </summary>
    public static async Task<string> UnicoAsync(
        string baseUsername,
        Func<string, Task<bool>> existe,
        int maxIntentos = 100)
    {
        if (!await existe(baseUsername)) return baseUsername;

        for (var i = 2; i <= maxIntentos; i++)
        {
            var candidato = $"{baseUsername}{i}";
            if (!await existe(candidato)) return candidato;
        }

        throw new InvalidOperationException(
            $"No se pudo generar un usuario único para '{baseUsername}' tras {maxIntentos} intentos.");
    }
}
