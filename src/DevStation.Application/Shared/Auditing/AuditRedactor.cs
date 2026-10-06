using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace DevStation.Application.Shared.Auditing;

/// <summary>
/// Read-side redaction for AuditEvent.Metadata: any key whose name matches
/// password|secret|token|key (case-insensitive) is emitted as "***".
/// Applied at read time so no secret can ever leave through /api/audit even
/// if a writer stored one.
/// </summary>
public static partial class AuditRedactor
{
    public static JsonNode? Redact(string? metadataJson)
    {
        if (string.IsNullOrEmpty(metadataJson)) return null;
        JsonNode? node;
        try { node = JsonNode.Parse(metadataJson); }
        catch { return null; }
        RedactNode(node);
        return node;
    }

    private static void RedactNode(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var (key, value) in obj.ToList())
                {
                    if (SensitiveKey().IsMatch(key))
                        obj[key] = "***";
                    else
                        RedactNode(value);
                }
                break;
            case JsonArray arr:
                foreach (var item in arr) RedactNode(item);
                break;
        }
    }

    [GeneratedRegex(@"(?i)(password|secret|token|key)")]
    private static partial Regex SensitiveKey();
}
