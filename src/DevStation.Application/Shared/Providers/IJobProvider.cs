using DevStation.Domain.Enums;

namespace DevStation.Application.Shared.Providers;

public sealed record JobInfo(
    Guid Id, string Name, string? Description, string? Schedule, string? Timezone,
    string Status, DateTimeOffset? LastRun, DateTimeOffset? NextRun,
    long? DurationMs, int SuccessCount, int FailureCount);

public sealed record JobExecutionInfo(
    Guid Id, DateTimeOffset StartedAt, DateTimeOffset? EndedAt, long? DurationMs,
    string Status, string? Error);

/// <summary>Job registry backed by Quartz (in-memory store); execution history lives
/// in job_executions. Public ids are JobDefinition guids (Quartz keys are names).</summary>
public interface IJobProvider
{
    Task<IReadOnlyList<JobInfo>> ListAsync(CancellationToken ct);
    Task<JobInfo?> GetAsync(Guid id, CancellationToken ct);
    Task TriggerAsync(Guid id, CancellationToken ct);
    Task PauseAsync(Guid id, CancellationToken ct);
    Task ResumeAsync(Guid id, CancellationToken ct);
}

public sealed record JobStatusUpdate(Guid JobId, string Name, string Status,
    DateTimeOffset? LastRun, DateTimeOffset? NextRun, long? DurationMs);

/// <summary>Publishes `job.updated` to /hubs/jobs; implemented in Web (SignalR).</summary>
public interface IJobNotifier
{
    Task Publish(JobStatusUpdate update, CancellationToken ct);
}

public static class JobStatusDeriver
{
    /// <summary>paused → never-run → failing → healthy, from trigger state + last run.</summary>
    public static string Derive(bool paused, DateTimeOffset? lastRun, JobStatus? lastStatus)
    {
        if (paused) return "paused";
        if (lastRun is null) return "never-run";
        return lastStatus == JobStatus.Failed ? "failing" : "healthy";
    }
}
