using DevStation.Domain.Enums;

namespace DevStation.Domain.Entities;

public class JobExecution
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid JobDefinitionId { get; set; }
    public JobStatus Status { get; set; } = JobStatus.Running;
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? EndedAt { get; set; }
    public long? DurationMs { get; set; }
    public string? Output { get; set; }
    public string? Error { get; set; }
}
