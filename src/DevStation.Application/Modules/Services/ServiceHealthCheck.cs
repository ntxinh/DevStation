using DevStation.Application.Shared.Persistence;
using DevStation.Application.Shared.Providers;
using DevStation.Domain.Entities;

namespace DevStation.Application.Modules.Services;

/// <summary>Probe a service's Url (http(s) scheme → http, else host:port → tcp)
/// and persist the result onto the entity. Shared by the health endpoint and
/// service restart (post-restart recheck).</summary>
public static class ServiceHealthCheck
{
    public static async Task<HealthResult> Recheck(IDevStationDb db,
        IHealthCheckExecutor executor, Service s, CancellationToken ct)
    {
        var kind = Uri.TryCreate(s.Url, UriKind.Absolute, out var uri) &&
                   uri.Scheme is "http" or "https" ? "http" : "tcp";
        var r = await executor.Execute(
            new HealthCheckDef(s.Id, kind, s.Url!, 5), ct);
        s.Status = r.Status;
        s.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return r;
    }
}
