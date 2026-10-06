using DevStation.Application.Shared.Persistence;
using DevStation.Application.Shared.Providers;
using DevStation.Application.Shared.Results;
using DevStation.Domain.Entities;
using DevStation.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;

namespace DevStation.Infrastructure.Jobs;

/// <summary>
/// IJobProvider over Quartz's in-memory store. JobDefinition rows (materialized
/// at startup / lazily by the listener) carry ids + run history; Quartz holds
/// the schedule.
/// </summary>
public sealed class QuartzJobProvider(
    IServiceScopeFactory scopeFactory,
    ISchedulerFactory schedulerFactory,
    IReadOnlyList<JobRegistration> registrations,
    ILogger<QuartzJobProvider> logger) : IJobProvider
{
    private ValueTask<IScheduler> Scheduler(CancellationToken ct)
        => schedulerFactory.GetScheduler(ct);

    public async Task<IReadOnlyList<JobInfo>> ListAsync(CancellationToken ct)
    {
        var scheduler = await Scheduler(ct);
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IDevStationDb>();

        var defs = await db.JobDefinitions.ToListAsync(ct);
        var infos = new List<JobInfo>(defs.Count + registrations.Count);
        foreach (var def in defs)
            infos.Add(await DescribeAsync(scheduler, db, def, ct));

        // registered job with no def row yet (pre-sync) — show it so it's operable
        var byName = defs.Select(d => d.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var reg in registrations.Where(r => !byName.Contains(r.Name)))
        {
            var (paused, nextRun) = await TriggerStateAsync(scheduler, reg.Key, ct);
            infos.Add(new JobInfo(Guid.Empty, reg.Name, reg.Description, reg.Cron,
                reg.Timezone, paused ? "paused" : "never-run", null, nextRun, null, 0, 0));
        }
        return infos.OrderBy(i => i.Name).ToList();
    }

    public async Task<JobInfo?> GetAsync(Guid id, CancellationToken ct)
    {
        var scheduler = await Scheduler(ct);
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IDevStationDb>();
        var def = await db.JobDefinitions.FirstOrDefaultAsync(d => d.Id == id, ct);
        return def is null ? null : await DescribeAsync(scheduler, db, def, ct);
    }

    // ponytail: per-def trigger-state + last-duration lookups — job count is tiny
    private static async Task<JobInfo> DescribeAsync(IScheduler scheduler,
        IDevStationDb db, JobDefinition def, CancellationToken ct)
    {
        var key = new JobKey(def.Name);
        var (paused, nextRun) = await scheduler.Exists(key, ct)
            ? await TriggerStateAsync(scheduler, key, ct)
            : (false, null);
        var duration = await db.JobExecutions
            .Where(e => e.JobDefinitionId == def.Id)
            .OrderByDescending(e => e.StartedAt)
            .Select(e => e.DurationMs)
            .FirstOrDefaultAsync(ct);
        return new JobInfo(def.Id, def.Name, def.Description, def.Cron, def.Timezone,
            JobStatusDeriver.Derive(paused, def.LastRunAt, def.Status),
            def.LastRunAt, nextRun, duration, def.SuccessCount, def.FailureCount);
    }

    /// <summary>Paused = every trigger paused; next run = earliest upcoming fire.</summary>
    private static async Task<(bool paused, DateTimeOffset? nextRun)> TriggerStateAsync(
        IScheduler scheduler, JobKey key, CancellationToken ct)
    {
        var triggers = (await scheduler.QueryTriggers(new TriggerQuery { Job = key }, ct)).Items;
        if (triggers.Count == 0) return (false, null);
        var paused = triggers.All(t => t.State is TriggerState.Paused
            or TriggerState.Complete or TriggerState.None);
        var next = triggers.Where(t => t.State == TriggerState.Normal)
            .Select(t => t.NextFireTimeUtc).Where(d => d is not null).Min();
        return (paused, next);
    }

    public Task TriggerAsync(Guid id, CancellationToken ct)
        => Mutate(id, ct, (s, k, c) => s.TriggerJob(k, null, c));

    public Task PauseAsync(Guid id, CancellationToken ct)
        => Mutate(id, ct, async (s, k, c) => await s.PauseJob(k, c));

    public Task ResumeAsync(Guid id, CancellationToken ct)
        => Mutate(id, ct, async (s, k, c) => await s.ResumeJob(k, c));

    private async Task Mutate(Guid id, CancellationToken ct,
        Func<IScheduler, JobKey, CancellationToken, ValueTask> op)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IDevStationDb>();
        var def = await db.JobDefinitions.FirstOrDefaultAsync(d => d.Id == id, ct)
            ?? throw new AppException("NOT_FOUND", 404, "Job not found");
        var scheduler = await Scheduler(ct);
        var key = new JobKey(def.Name);
        if (!await scheduler.Exists(key, ct))
            throw new AppException("JOB_NOT_SCHEDULED", 422,
                $"job '{def.Name}' is not registered in this instance's Quartz scheduler");
        await op(scheduler, key, ct);
        await PublishAsync(scheduler, def, ct);
    }

    private async Task PublishAsync(IScheduler scheduler, JobDefinition def,
        CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var notifier = scope.ServiceProvider.GetService<IJobNotifier>();
        if (notifier is null) return;
        try
        {
            var (paused, next) = await TriggerStateAsync(scheduler, new JobKey(def.Name), ct);
            await notifier.Publish(new JobStatusUpdate(def.Id, def.Name,
                JobStatusDeriver.Derive(paused, def.LastRunAt, def.Status),
                def.LastRunAt, next, null), ct);
        }
        catch (Exception ex) // hub unreachable must not fail a successful mutation
        {
            logger.LogWarning(ex, "job.updated publish failed");
        }
    }
}
