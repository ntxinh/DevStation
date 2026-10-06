using DevStation.Domain.Enums;

namespace DevStation.Domain.Entities;

public class JobDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string Type { get; set; } = "";       // key → registered Quartz IJob impl (appsettings Jobs: array)
    public string? Cron { get; set; }
    public string? Timezone { get; set; }
    public JobStatus Status { get; set; } = JobStatus.Idle;
    public DateTimeOffset? LastRunAt { get; set; }
    public DateTimeOffset? NextRunAt { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
