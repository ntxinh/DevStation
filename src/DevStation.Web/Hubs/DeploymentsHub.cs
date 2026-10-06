using DevStation.Application.Shared.Providers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace DevStation.Web.Hubs;

[Authorize("Read")]
public sealed class DeploymentsHub : Hub;

/// <summary>Bridges IDeploymentNotifier → SignalR `deployment.updated` on /hubs/deployments.</summary>
public sealed class SignalRDeploymentNotifier(IHubContext<DeploymentsHub> hub)
    : IDeploymentNotifier
{
    public Task Publish(DeploymentUpdate update, CancellationToken ct)
        => hub.Clients.All.SendAsync("deployment.updated", new
        {
            deploymentId = update.DeploymentId,
            status = update.Status.ToString().ToLowerInvariant(),
            update.StartedAt,
            update.FinishedAt,
        }, ct);
}
