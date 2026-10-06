using DevStation.Domain.Enums;

namespace DevStation.Domain.Entities;

public class HealthCheckResult
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid HealthCheckId { get; set; }
    public HealthStatus Status { get; set; }
    public int? LatencyMs { get; set; }
    public string? Error { get; set; }
    public DateTimeOffset CheckedAt { get; set; } = DateTimeOffset.UtcNow;
}
