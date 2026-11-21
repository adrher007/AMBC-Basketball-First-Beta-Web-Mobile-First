using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Ambc.Api.Services;

/// <summary>
/// Cliente de Groq (chat completions, compatible OpenAI).
/// Extrae datos de la planilla de básquet a JSON estructurado.
/// Modo degradado: sin plantilla de referencia. Modo completo: con plantilla + ejemplo.
/// </summary>
public class GroqService
{
    private readonly IConfiguration _cfg;
    private readonly HttpClient _http;

    public GroqService(IConfiguration cfg, HttpClient http)
    {
        _cfg = cfg;
        _http = http;
    }

    /// <summary>
    /// Devuelve el JSON extraído por Groq (string). Lanza excepción si no hay API key.
    /// </summary>
    public async Task<string> ExtraerPlanilla(
        string imagenBase64,
        bool modoCompleto,
        string? plantillaBase64 = null)
    {
        var apiKey = _cfg["Groq:ApiKey"];
        // Placeholder del repo (sin clave real): se trata como "no configurada"
        if (string.IsNullOrWhiteSpace(apiKey) || apiKey.StartsWith("REEMPLAZAR_", StringComparison.Ordinal))
            throw new InvalidOperationException("API key de Groq no configurada.");

        var model = _cfg["Groq:Model"] ?? "meta-llama/llama-4-scout-17b-16e-instruct";

        var systemPrompt = """
            OBJETIVO:
            Leer planillas de la Asociación Municipal de Basquetbol Cochabamba (AMBC).

            DATOS A EXTRAER:
            1. Categoría, Rama, Fecha (DD-MM-AA → Día de Mes de Año).
            2. Equipo A y B (nombres).
            3. Jugadores de ambos equipos: nombre y número de playera (el número está al lado del nombre).
            4. Puntuación de Ejecución (casillas 1-160):
               - 8 columnas intercaladas A/B.
               - Numeración: 1 al 40, 41 al 80, 81 al 120, 121 al 160.
               - Regla de anotación:
                 * Solo se tacha la ÚLTIMA casilla del grupo.
                 * Las anteriores quedan EN BLANCO (no se marcan).
                 * Al lado de la tachada se escribe el # del jugador.
               - Tipos: Libre (1) → se tacha 1; Doble (2) → se tacha la 2da; Triple (3) → se tacha la 3ra.
            5. Puntuación por cuartos: 1°, 2°, 3°, 4°, Prórroga (Equipo A y B) —se lee de la tabla de cuartos—.
            6. Puntuación Final: Suma de todos los cuartos (Equipo A y B).

            REGLAS DE ORO:
            - La fecha está en DD-MM-AA.
            - El número de playera está al lado del nombre.
            - Solo se tacha la última casilla del grupo.
            - Las anteriores quedan en blanco.
            - Al lado de la tachada se escribe el # del jugador.
            - Los cuartos NO se deducen de la numeración de casillas; se leen de la tabla de puntuación por cuartos.
            - Dato incierto o ilegible = null. NUNCA conviertas null a 0. NUNCA inventes datos.

            FORMATO DE SALIDA (JSON):
            {
              "schemaVersion": 1,
              "match": { "category": "string", "branch": "string", "date": { "day": int, "month": int, "year": int, "full": "DD-MM-AA" } },
              "teams": {
                "A": { "name": "string", "players": [{"number": int|null, "name": "string"}] },
                "B": { "name": "string", "players": [{"number": int|null, "name": "string"}] }
              },
              "scores": {
                "execution": { "cells": [{"number": int, "team": "A|B", "isCrossed": bool, "player": int|null, "points": int|null}] },
                "quarters": { "1": {"A": int, "B": int}, "2": {"A": int, "B": int}, "3": {"A": int, "B": int}, "4": {"A": int, "B": int}, "overtime": {"A": int|null, "B": int|null} },
                "final": { "A": int, "B": int }
              }
            }
            """;

        var messages = new List<object>
        {
            new { role = "system", content = systemPrompt },
        };

        if (modoCompleto && !string.IsNullOrWhiteSpace(plantillaBase64))
        {
            messages.Add(new
            {
                role = "user",
                content = new object[]
                {
                    new { type = "text", text = "PLANTILLA OFICIAL AMBC (imagen de referencia vacía)" },
                    new { type = "image_url", image_url = new { url = $"data:image/jpeg;base64,{plantillaBase64}" } },
                },
            });
            messages.Add(new
            {
                role = "assistant",
                content = """{"schemaVersion":1,"match":{"category":"Honor Masculino","branch":"Masculino","date":{"day":25,"month":8,"year":26,"full":"25-08-26"}},"teams":{"A":{"name":"Equipo A","players":[{"number":7,"name":"Juan Perez"}]},"B":{"name":"Equipo B","players":[{"number":12,"name":"Carlo Mendez"}]}},"scores":{"execution":{"cells":[]},"quarters":{"1":{"A":20,"B":15},"2":{"A":18,"B":19},"3":{"A":22,"B":21},"4":{"A":19,"B":18},"overtime":{"A":null,"B":null}},"final":{"A":79,"B":73}}}""",
            });
        }

        messages.Add(new
        {
            role = "user",
            content = new object[]
            {
                new { type = "text", text = "Extrae datos" },
                new { type = "image_url", image_url = new { url = $"data:image/jpeg;base64,{imagenBase64}" } },
            },
        });

        var body = new
        {
            model,
            messages,
            temperature = 0,
            response_format = new { type = "json_object" },
            max_tokens = 1500,
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/chat/completions");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var resp = await _http.SendAsync(req, cts.Token);
        resp.EnsureSuccessStatusCode();

        var json = await resp.Content.ReadAsStringAsync(cts.Token);
        using var doc = JsonDocument.Parse(json);
        var content = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        return content ?? "";
    }
}
