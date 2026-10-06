using DevStation.Domain.Enums;

namespace DevStation.Domain.Entities;

public class Service
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? EnvironmentId { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string? Type { get; set; } // web|api|worker|database|...
    public string? Url { get; set; }
    public string? ContainerId { get; set; } // runtime container this service maps to (restart via container)
    public string? Version { get; set; }
    public HealthStatus Status { get; set; } = HealthStatus.Unknown;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
