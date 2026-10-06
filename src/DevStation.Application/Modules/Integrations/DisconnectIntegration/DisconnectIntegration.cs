using System.Security.Claims;
using DevStation.Application.Shared.Auditing;
using DevStation.Application.Shared.Persistence;
using DevStation.Application.Shared.Providers;
using DevStation.Application.Shared.Secrets;
using DevStation.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DevStation.Application.Modules.Integrations.DisconnectIntegration;

public sealed class DisconnectIntegrationHandler(IDevStationDb db,
    IEnumerable<IIntegration> integrations, ISecretStore secrets,
    IAuditWriter audit)
{
    /// <summary>Clears stored secrets + row state. Env-provided secrets can't
    /// be deleted — the row still flips to notconfigured so the UI shows
    /// the disconnected state.</summary>
    public async Task Handle(string type, ClaimsPrincipal user, CancellationToken ct)
    {
        var intg = integrations.For(type);
        foreach (var k in intg.SecretKeys)
            await secrets.DeleteAsync(IntegrationCatalog.SecretKey(intg.Type, k), ct);

        var row = await db.Integrations.FirstOrDefaultAsync(
            x => x.Type == intg.Type, ct);
        if (row is not null)
        {
            row.Status = IntegrationStatus.NotConfigured;
            row.Config = null;
            row.CredentialsRef = null;
            row.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
        }
        await audit.Record("integration.disconnect", "integration", row?.Id,
            user.Identity?.Name, "ok", new { type = intg.Type }, ct);
    }
}
