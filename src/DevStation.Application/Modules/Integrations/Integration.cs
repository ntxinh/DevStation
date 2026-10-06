using System.Text.Json;
using DevStation.Application.Shared.Providers;
using DevStation.Application.Shared.Secrets;
using DevStation.Domain.Entities;

namespace DevStation.Application.Modules.Integrations;

internal static class IntegrationEntityExt
{
    public static IReadOnlyDictionary<string, string>? ParsedConfig(this Integration i)
        => i.Config is null ? null
            : JsonSerializer.Deserialize<Dictionary<string, string>>(i.Config);

    /// <summary>Credentials = stored SecretRef resolves, or (no row/ref yet)
    /// the convention key intg-{type}-{firstSecret} exists — env-provided
    /// secrets count.</summary>
    public static async Task<bool> HasCredentialsAsync(this Integration? row,
        IIntegration intg, ISecretStore secrets, CancellationToken ct)
    {
        var key = row?.CredentialsRef?.Key
            ?? (intg.SecretKeys.Count > 0
                ? IntegrationCatalog.SecretKey(intg.Type, intg.SecretKeys[0])
                : null);
        return key is not null && await secrets.GetAsync(key, ct) is not null;
    }
}
