using DevStation.Domain.Enums;

namespace DevStation.Application.Shared.Providers;

public sealed record DeploymentStatusInfo(
    DeploymentStatus Status,
    IReadOnlyList<string> Logs,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    string? Commit,
    string? Version);

/// <summary>Deployment backend seam. The mock provider drives an in-memory
/// state machine; the GitHub Actions provider dispatches workflow runs.
/// Implementations dual-write the <c>deployments</c> table so history
/// survives restarts.</summary>
public interface IDeploymentProvider
{
    /// <summary>Creates the deployment row (queued) and starts progression.</summary>
    Task<Guid> StartAsync(Guid? projectId, Guid? environmentId,
        string? commit, CancellationToken ct);
    /// <summary>Transitions queued/running → cancelled; 404 unknown, 409 finished.</summary>
    Task CancelAsync(Guid id, CancellationToken ct);
    Task<DeploymentStatusInfo> GetStatusAsync(Guid id, CancellationToken ct);
}

public sealed record DeploymentUpdate(Guid DeploymentId, DeploymentStatus Status,
    DateTimeOffset? StartedAt, DateTimeOffset? FinishedAt);

/// <summary>Publishes `deployment.updated` to /hubs/deployments; implemented in Web (SignalR).</summary>
public interface IDeploymentNotifier
{
    Task Publish(DeploymentUpdate update, CancellationToken ct);
}
