using DevStation.Domain.Entities;

namespace DevStation.Application.Modules.Health;

public sealed record HealthResultDto(string Status, int? LatencyMs, string? Error,
    DateTimeOffset CheckedAt)
{
    public static HealthResultDto Of(HealthCheckResult r) => new(
        r.Status.ToString().ToLowerInvariant(), r.LatencyMs, r.Error, r.CheckedAt);
}

public sealed record HealthCheckDto(Guid Id, string Name, string Kind, string Target,
    Guid? EnvironmentId, Guid? ServiceId, int IntervalSeconds, int TimeoutSeconds,
    bool Enabled, HealthResultDto? Last)
{
    public static HealthCheckDto Of(HealthCheck h, HealthCheckResult? last = null) => new(
        h.Id, h.Name, h.Kind, h.Target, h.EnvironmentId, h.ServiceId,
        h.IntervalSeconds, h.TimeoutSeconds, h.Enabled,
        last is null ? null : HealthResultDto.Of(last));
}
