using DevStation.Domain.Enums;

namespace DevStation.Domain.Entities;

public class Deployment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? ProjectId { get; set; }
    public Guid? EnvironmentId { get; set; }
    public string? Commit { get; set; }
    public string? Version { get; set; }
    public DeploymentStatus Status { get; set; } = DeploymentStatus.Queued;
    public string? ProviderDeploymentId { get; set; } // external id (e.g. GitHub Actions run)
    public string? Log { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public string? TriggeredBy { get; set; }
}
