using DevStation.Application.Shared.Auditing;
using DevStation.Application.Shared.Persistence;
using DevStation.Application.Shared.Providers;
using DevStation.Domain.Entities;
using DevStation.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;

namespace DevStation.Infrastructure.Jobs;

/// <summary>
/// Every finished job execution → job_executions row + counters on job_definitions
/// + `job.executed` audit + `job.updated` broadcast on /hubs/jobs.
/// </summary>
public sealed class AuditingJobListener(IServiceScopeFactory scopeFactory,
    ILogger<AuditingJobListener> logger) : IJobListener
{
    public string Name => "auditing";

    public ValueTask JobToBeExecuted(IJobExecutionContext context,
        CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public ValueTask JobExecutionVetoed(IJobExecutionContext context,
        CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public ValueTask JobWasExecuted(IJobExecutionContext context,
        JobExecutionException? jobException, CancellationToken cancellationToken)
    {
        var jobType = context.JobDetail.JobType.TryResolve(out var jt) ? jt : null;
        return Record(context.JobDetail.Key.Name, jobType,
            context.FireTimeUtc, context.JobRunTime, jobException?.ToString(),
            cancellationToken);
    }

    /// <summary>Split from JobWasExecuted so unit tests can drive it without
    /// fabricating an IJobExecutionContext.</summary>
    public async ValueTask Record(string jobName, Type? jobType,
        DateTimeOffset startedAt, TimeSpan runTime, string? error,
        CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<IDevStationDb>();

        var def = await db.JobDefinitions
            .FirstOrDefaultAsync(d => d.Name == jobName, ct);
        if (def is null) // fired before sync / def deleted — materialize on the fly
        {
            def = new JobDefinition { Name = jobName, Type = BuiltInJobs.KeyFor(jobType) };
            db.JobDefinitions.Add(def);
        }
        error = Truncate(error);
        var ok = error is null;
        var finished = startedAt + runTime;
        def.LastRunAt = finished;
        def.Status = ok ? JobStatus.Succeeded : JobStatus.Failed;
        if (ok) def.SuccessCount++; else def.FailureCount++;

        db.JobExecutions.Add(new JobExecution
        {
            JobDefinitionId = def.Id,
            Status = ok ? JobStatus.Succeeded : JobStatus.Failed,
            StartedAt = startedAt,
            EndedAt = finished,
            DurationMs = (long)runTime.TotalMilliseconds,
            Error = error,
        });
        await db.SaveChangesAsync(ct);

        await sp.GetRequiredService<IAuditWriter>().Record("job.executed", "job",
            def.Id, null, ok ? "ok" : "error", new { jobName, error }, ct);

        var notifier = sp.GetService<IJobNotifier>();
        if (notifier is not null)
        {
            try
            {
                await notifier.Publish(new JobStatusUpdate(def.Id, def.Name,
                    JobStatusDeriver.Derive(false, def.LastRunAt, def.Status),
                    def.LastRunAt, null, (long)runTime.TotalMilliseconds), ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "job.updated publish failed");
            }
        }
    }

    internal static string? Truncate(string? s)
        => s is null ? null : s.Length <= 4000 ? s : s[..4000];
}
