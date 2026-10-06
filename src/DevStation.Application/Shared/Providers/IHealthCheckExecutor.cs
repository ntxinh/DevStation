using DevStation.Domain.Enums;

namespace DevStation.Application.Shared.Providers;

public sealed record HealthCheckDef(Guid Id, string Kind, string Target, int TimeoutSeconds);

public sealed record HealthResult(HealthStatus Status, int? LatencyMs, string? Error,
    DateTimeOffset CheckedAt);

/// <summary>SignalR/health-update payload — what `health.updated` broadcasts.</summary>
public sealed record HealthCheckUpdate(Guid HealthCheckId, HealthStatus Status,
    int? LatencyMs, string? Error, DateTimeOffset CheckedAt);

public interface IHealthCheckExecutor
{
    Task<HealthResult> Execute(HealthCheckDef check, CancellationToken ct);
}

/// <summary>Publishes a fresh check result to live subscribers (/hubs/health).
/// Implemented in Web (SignalR); absent → polling still persists results.</summary>
public interface IHealthCheckNotifier
{
    Task Publish(HealthCheckUpdate update, CancellationToken ct);
}
