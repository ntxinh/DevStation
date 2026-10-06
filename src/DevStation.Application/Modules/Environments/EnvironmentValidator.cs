using DevStation.Domain.Entities;

namespace DevStation.Application.Modules.Environments;

public static class EnvironmentValidator
{
    public static readonly string[] Types = ["dev", "development", "qa", "staging", "production"];

    public static void Check(List<string> errors, string? name, string? type,
        string? baseUrl, string? healthCheckUrl)
    {
        var trimmed = name?.Trim();
        if (trimmed is null or "" or { Length: > 200 })
            errors.Add("name must be 1-200 characters");
        if (type is { Length: > 0 } t && !Types.Contains(t))
            errors.Add($"type must be one of: {string.Join("|", Types)}");
        foreach (var (label, url) in new[] { ("baseUrl", baseUrl), ("healthCheckUrl", healthCheckUrl) })
            if (url is { Length: > 0 } u &&
                !(Uri.TryCreate(u, UriKind.Absolute, out var uri) &&
                  uri.Scheme is "http" or "https"))
                errors.Add($"{label} must be an http(s) URL");
    }
}
