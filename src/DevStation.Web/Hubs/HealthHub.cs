using DevStation.Application.Shared.Providers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace DevStation.Web.Hubs;

[Authorize("Read")]
public sealed class HealthHub : Hub;

/// <summary>Bridges IHealthCheckNotifier → SignalR `health.updated` on /hubs/health.</summary>
public sealed class SignalRHealthNotifier(IHubContext<HealthHub> hub) : IHealthCheckNotifier
{
    public Task Publish(HealthCheckUpdate update, CancellationToken ct)
        => hub.Clients.All.SendAsync("health.updated", new
        {
            healthCheckId = update.HealthCheckId,
            status = update.Status.ToString().ToLowerInvariant(),
            update.LatencyMs,
            update.Error,
            update.CheckedAt,
        }, ct);
}
