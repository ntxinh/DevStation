using System.Security.Claims;
using System.Text.Json;
using DevStation.Application.Shared.Auditing;
using DevStation.Application.Shared.Persistence;
using DevStation.Application.Shared.Providers;
using DevStation.Application.Shared.Results;
using DevStation.Application.Shared.Secrets;
using DevStation.Domain.Entities;
using DevStation.Domain.Enums;
using DevStation.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace DevStation.Application.Modules.Integrations.ConnectIntegration;

public sealed record ConnectIntegrationRequest
{
    /// <summary>Non-secret settings only; keys must be in the provider's ConfigKeys.</summary>
    public Dictionary<string, string> Config { get; init; } = new();
    /// <summary>Secret name → value; names must be in the provider's SecretKeys.</summary>
    public Dictionary<string, string> Secrets { get; init; } = new();
}

public sealed class ConnectIntegrationHandler(IDevStationDb db,
    IEnumerable<IIntegration> integrations, ISecretStore secrets,
    IAuditWriter audit)
{
    public async Task<IntegrationDto> Handle(string type,
        ConnectIntegrationRequest r, ClaimsPrincipal user, CancellationToken ct)
    {
        var intg = integrations.For(type);
        var errors = new List<string>();
        foreach (var k in r.Config.Keys)
            if (!intg.ConfigKeys.Contains(k, StringComparer.OrdinalIgnoreCase))
                errors.Add($"unknown config key '{k}' (allowed: {string.Join(", ", intg.ConfigKeys)})");
        foreach (var (k, v) in r.Secrets)
        {
            if (!intg.SecretKeys.Contains(k, StringComparer.OrdinalIgnoreCase))
                errors.Add($"unknown secret key '{k}' (allowed: {string.Join(", ", intg.SecretKeys)})");
            else if (string.IsNullOrWhiteSpace(v))
                errors.Add($"secret '{k}' must not be empty");
        }
        if (errors.Count > 0)
            throw new AppException("VALIDATION_ERROR", 400, string.Join("; ", errors));

        await intg.ConfigureAsync(new IntegrationConfig(r.Config), ct);

        var storedKeys = new List<string>();
        foreach (var (k, v) in r.Secrets)
        {
            var sk = IntegrationCatalog.SecretKey(intg.Type, k);
            await secrets.SetAsync(sk, v, ct);
            storedKeys.Add(sk);
        }

        var row = await db.Integrations.FirstOrDefaultAsync(
            x => x.Type == intg.Type, ct);
        if (row is null)
        {
            row = new Integration { Name = intg.Name, Type = intg.Type };
            db.Integrations.Add(row);
        }
        row.Config = JsonSerializer.Serialize(r.Config);
        row.Status = IntegrationStatus.Configured;
        if (storedKeys.Count > 0)
            row.CredentialsRef = new SecretRef(storedKeys[0]);
        row.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        // metadata keys only — never values
        await audit.Record("integration.connect", "integration", row.Id,
            user.Identity?.Name, "ok",
            new { type = intg.Type, configKeys = r.Config.Keys, secretKeys = r.Secrets.Keys }, ct);
        return IntegrationDto.Of(row,
            await row.HasCredentialsAsync(intg, secrets, ct),
            intg.ConfigKeys, intg.SecretKeys);
    }
}
