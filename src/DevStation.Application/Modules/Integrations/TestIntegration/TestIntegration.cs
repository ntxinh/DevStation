using System.Security.Claims;
using DevStation.Application.Shared.Auditing;
using DevStation.Application.Shared.Persistence;
using DevStation.Application.Shared.Providers;
using DevStation.Domain.Entities;
using DevStation.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DevStation.Application.Modules.Integrations.TestIntegration;

public sealed record IntegrationTestResult(bool Ok, string Status);

public sealed class TestIntegrationHandler(IDevStationDb db,
    IEnumerable<IIntegration> integrations, IAuditWriter audit)
{
    /// <summary>Runs the provider's live probe; persists the resulting status.
    /// An unconfigured integration returns {ok:false, status:notconfigured}.</summary>
    public async Task<IntegrationTestResult> Handle(string type,
        ClaimsPrincipal user, CancellationToken ct)
    {
        var intg = integrations.For(type);
        var row = await db.Integrations.FirstOrDefaultAsync(
            x => x.Type == intg.Type, ct);

        // hydrate the singleton from persisted config — survives restarts
        if (row?.ParsedConfig() is { } cfg)
            await intg.ConfigureAsync(new IntegrationConfig(
                new Dictionary<string, string>(cfg)), ct);

        var ok = await intg.TestAsync(ct);

        if (row is null && intg.Status != IntegrationStatus.NotConfigured)
        {
            // env-only credentials: persist the first row so status is visible
            row = new Integration { Name = intg.Name, Type = intg.Type };
            db.Integrations.Add(row);
        }
        if (row is not null)
        {
            row.Status = intg.Status;
            if (ok) row.LastSyncAt = DateTimeOffset.UtcNow;
            row.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        await audit.Record("integration.test", "integration", row?.Id,
            user.Identity?.Name, ok ? "ok" : "error",
            new { type = intg.Type, status = intg.Status.ToString() }, ct);
        return new IntegrationTestResult(ok, intg.Status.ToString().ToLowerInvariant());
    }
}
