using DevStation.Application.Shared.Providers;
using DevStation.Application.Shared.Results;
using DevStation.Application.Shared.Secrets;
using DevStation.Domain.Enums;

namespace DevStation.Infrastructure.Integrations;

/// <summary>Shared IIntegration plumbing: required-key validation, secret
/// retrieval, status bookkeeping.</summary>
public abstract class IntegrationBase : IIntegration
{
    protected IntegrationBase(string name, string type, ISecretStore secrets,
        IReadOnlyList<string> configKeys, IReadOnlyList<string> secretKeys)
    {
        Name = name; Type = type; Secrets = secrets;
        ConfigKeys = configKeys; SecretKeys = secretKeys;
    }

    public string Name { get; }
    public string Type { get; }
    public IntegrationStatus Status { get; protected set; } = IntegrationStatus.NotConfigured;
    public IReadOnlyList<string> ConfigKeys { get; }
    public IReadOnlyList<string> SecretKeys { get; }
    protected ISecretStore Secrets { get; }
    protected IntegrationConfig Config { get; private set; } = new();

    /// <summary>Non-secret keys that must be present and non-empty.</summary>
    protected virtual IReadOnlyList<string> RequiredConfigKeys => [];

    public virtual Task ConfigureAsync(IntegrationConfig cfg, CancellationToken ct)
    {
        var missing = RequiredConfigKeys
            .Where(k => !cfg.TryGetValue(k, out var v) || string.IsNullOrWhiteSpace(v))
            .ToList();
        if (missing.Count > 0)
            throw new AppException("VALIDATION_ERROR", 400,
                $"{Type} requires config: {string.Join(", ", missing)}");
        Config = cfg;
        Status = IntegrationStatus.Configured;
        return Task.CompletedTask;
    }

    public abstract Task<bool> TestAsync(CancellationToken ct);

    /// <summary>Reads this integration's secret; null → marks NotConfigured.</summary>
    protected async Task<string?> Secret(string key, CancellationToken ct)
    {
        var v = await Secrets.GetAsync(IntegrationCatalog.SecretKey(Type, key), ct);
        if (v is null) Status = IntegrationStatus.NotConfigured;
        return v;
    }

    protected bool Fail(IntegrationStatus status = IntegrationStatus.Error)
    {
        Status = status;
        return false;
    }
}
