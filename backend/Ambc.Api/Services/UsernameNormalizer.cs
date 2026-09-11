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
}
