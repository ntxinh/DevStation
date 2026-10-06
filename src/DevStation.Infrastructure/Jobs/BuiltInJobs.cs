using DevStation.Application.Shared.Persistence;
using DevStation.Application.Shared.Providers;
using DevStation.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace DevStation.Infrastructure.Jobs;

/// <summary>Config `type` key → Quartz job type. `Jobs:` entries may only pick
/// from here — no user-defined shell commands.</summary>
public static class BuiltInJobs
{
    public static readonly IReadOnlyDictionary<string, Type> Registry =
        new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
        {
            ["health-sweep"] = typeof(HealthSweepJob),
            ["log-cleanup"] = typeof(LogCleanupJob),
        };

    /// <summary>Inverse lookup for materializing JobDefinition.Type.</summary>
    public static string KeyFor(Type? t)
        => Registry.FirstOrDefault(kv => kv.Value == t).Key ?? t?.Name ?? "unknown";
}

/// <summary>Runs every enabled health check once — same effect as one poller tick
/// (persist result + broadcast + prune old rows).</summary>
public sealed class HealthSweepJob(IServiceScopeFactory scopeFactory,
    IHealthCheckExecutor executor) : IJob
{
    public async ValueTask Execute(IJobExecutionContext context, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IDevStationDb>();
        var notifier = scope.ServiceProvider.GetService<IHealthCheckNotifier>();


        var checks = await db.HealthChecks.Where(h => h.Enabled).ToListAsync(ct);
        if (checks.Count > 0)
        {
            // run checks off the DbContext (not thread-safe) — persist + publish after
            var results = await Task.WhenAll(checks.Select(async h =>
                (Check: h, Result: await executor.Execute(
                    new HealthCheckDef(h.Id, h.Kind, h.Target, h.TimeoutSeconds), ct))));

            foreach (var (h, r) in results)
                db.HealthCheckResults.Add(new HealthCheckResult
                {
                    HealthCheckId = h.Id,
                    Status = r.Status,
                    LatencyMs = r.LatencyMs,
                    Error = r.Error,
                    CheckedAt = r.CheckedAt,
                });
            await db.SaveChangesAsync(ct);

            if (notifier is not null)
                foreach (var (h, r) in results)
                    await notifier.Publish(new HealthCheckUpdate(h.Id, r.Status,
                        r.LatencyMs, r.Error, r.CheckedAt), ct);
        }

        // retention: same 7-day prune the poller does per tick
        var cutoff = DateTimeOffset.UtcNow.AddDays(-7);
        await db.HealthCheckResults.Where(r => r.CheckedAt < cutoff)
            .ExecuteDeleteAsync(ct);
    }
}

/// <summary>Prunes health_check_results older than 7 days (kept alive by the
/// poller too — this is the belt to its suspenders for long-idle instances).</summary>
public sealed class LogCleanupJob(IServiceScopeFactory scopeFactory) : IJob
{
    public async ValueTask Execute(IJobExecutionContext context, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IDevStationDb>();
        var cutoff = DateTimeOffset.UtcNow.AddDays(-7);
        await db.HealthCheckResults.Where(r => r.CheckedAt < cutoff)
            .ExecuteDeleteAsync(ct);
    }
}
