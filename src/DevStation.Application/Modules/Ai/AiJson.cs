using System.Text.Json;
using System.Text.Json.Serialization;

namespace DevStation.Application.Modules.Ai;

/// <summary>
/// Loose response parsing for model output: strips ```json fences and any
/// prose surrounding the first {...} block, then deserializes.
/// </summary>
public static class AiJson
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Returns the first balanced {...} region of the text,
    /// markdown fences removed; null when no JSON object is present.</summary>
    public static string? ExtractJsonBlock(string raw)
    {
        var s = raw.Trim();
        // fenced block wins when present
        var fence = s.IndexOf("```", StringComparison.Ordinal);
        if (fence >= 0)
        {
            var contentStart = s.IndexOf('\n', fence);
            var fenceEnd = s.LastIndexOf("```", StringComparison.Ordinal);
            if (contentStart > fence && fenceEnd > contentStart)
                s = s[(contentStart + 1)..fenceEnd].Trim();
        }

        var start = s.IndexOf('{');
        if (start < 0) return null;
        var depth = 0;
        var inString = false;
        var escape = false;
        for (var i = start; i < s.Length; i++)
        {
            var c = s[i];
            if (inString)
            {
                if (escape) escape = false;
                else if (c == '\\') escape = true;
                else if (c == '"') inString = false;
                continue;
            }
            if (c == '"') inString = true;
            else if (c == '{') depth++;
            else if (c == '}' && --depth == 0)
                return s[start..(i + 1)];
        }
        return null;
    }

    /// <summary>Parse the first JSON object in model output, or null.</summary>
    public static T? Parse<T>(string raw) where T : class
    {
        var block = ExtractJsonBlock(raw);
        if (block is null) return null;
        try { return JsonSerializer.Deserialize<T>(block, Opts); }
        catch (JsonException) { return null; }
    }
}
