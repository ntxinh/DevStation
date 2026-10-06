using DevStation.Application.Shared.Persistence;
using DevStation.Application.Shared.Providers;
using DevStation.Application.Shared.Secrets;
using DevStation.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DevStation.Application.Modules.Integrations.ListIntegrations;

public sealed class ListIntegrationsHandler(IDevStationDb db,
    IEnumerable<IIntegration> integrations, ISecretStore secrets)
{
    /// <summary>Registered provider types merged with persisted rows —
    /// unconfigured types still appear so the UI can offer connect.</summary>
    public async Task<List<IntegrationDto>> Handle(CancellationToken ct)
    {
        var rows = await db.Integrations.ToListAsync(ct);
        var result = new List<IntegrationDto>();
        foreach (var intg in integrations.OrderBy(i => i.Name))
        {
            var row = rows.FirstOrDefault(r =>
                string.Equals(r.Type, intg.Type, StringComparison.OrdinalIgnoreCase));
            var hasCredentials = await row.HasCredentialsAsync(intg, secrets, ct);
            result.Add(row is null
                ? IntegrationDto.Of(new Integration { Name = intg.Name, Type = intg.Type },
                    hasCredentials, intg.ConfigKeys, intg.SecretKeys)
                : IntegrationDto.Of(row, hasCredentials,
                    intg.ConfigKeys, intg.SecretKeys));
        }
        return result;
    }
}
