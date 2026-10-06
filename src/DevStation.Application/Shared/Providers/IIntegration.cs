using DevStation.Domain.Enums;

namespace DevStation.Application.Shared.Providers;

/// <summary>Non-secret integration settings. Secrets never live here — they go
/// to ISecretStore under <c>intg-{type}-{key}</c>.</summary>
public sealed class IntegrationConfig : Dictionary<string, string>
{
    public IntegrationConfig() : base(StringComparer.OrdinalIgnoreCase) { }
    public IntegrationConfig(IDictionary<string, string> d)
        : base(d, StringComparer.OrdinalIgnoreCase) { }
}

/// <summary>Pluggable external-service integration (github, jira, azure, cloudflare).</summary>
public interface IIntegration
{
    string Name { get; }
    string Type { get; }
    IntegrationStatus Status { get; }

    /// <summary>Non-secret keys this integration accepts in connect config.</summary>
    IReadOnlyList<string> ConfigKeys { get; }

    /// <summary>Secret keys this integration accepts; stored under intg-{type}-{key}.
    /// The first key is the primary credential used for SecretRef/hasCredentials.</summary>
    IReadOnlyList<string> SecretKeys { get; }

    Task ConfigureAsync(IntegrationConfig cfg, CancellationToken ct);
    Task<bool> TestAsync(CancellationToken ct);
}

public static class IntegrationCatalog
{
    public static string SecretKey(string type, string key) => $"intg-{type}-{key}";

    /// <summary>Maps {type} → registered integration; throws UNKNOWN_INTEGRATION_TYPE.</summary>
    public static IIntegration For(this IEnumerable<IIntegration> integrations, string type)
        => integrations.FirstOrDefault(i =>
               string.Equals(i.Type, type, StringComparison.OrdinalIgnoreCase))
           ?? throw new Results.AppException("UNKNOWN_INTEGRATION_TYPE", 400,
               $"unknown integration type '{type}'");
}
