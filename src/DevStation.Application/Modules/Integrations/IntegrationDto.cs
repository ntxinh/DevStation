using DevStation.Domain.Entities;

namespace DevStation.Application.Modules.Integrations;

/// <summary>
/// Integration as exposed over the API: non-secret config is returned verbatim,
/// credentials surface only as <c>hasCredentials</c> — neither secret values
/// nor SecretRef keys leave the server.
/// </summary>
public sealed record IntegrationDto(Guid? Id, string Name, string Type,
    string Status, IReadOnlyDictionary<string, string>? Config,
    bool HasCredentials, IReadOnlyList<string> ConfigKeys,
    IReadOnlyList<string> SecretKeys, DateTimeOffset? LastSyncAt,
    DateTimeOffset? UpdatedAt)
{
    public static IntegrationDto Of(Integration i, bool hasCredentials,
        IReadOnlyList<string> configKeys, IReadOnlyList<string> secretKeys)
        => new(i.Id, i.Name, i.Type,
            i.Status.ToString().ToLowerInvariant(), i.ParsedConfig(),
            hasCredentials, configKeys, secretKeys, i.LastSyncAt, i.UpdatedAt);
}
