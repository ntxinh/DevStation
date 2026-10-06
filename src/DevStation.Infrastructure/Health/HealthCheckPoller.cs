using DevStation.Application.Shared.Persistence;
using DevStation.Application.Shared.Providers;
using DevStation.Application.Shared.Notifications;
using DevStation.Domain.Entities;
using DevStation.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DevStation.Infrastructure.Health;

/// <summary>
/// Polls every enabled health check on its own IntervalSeconds, persists a
/// HealthCheckResult, and broadcasts `health.updated` via IHealthCheckNotifier.
/// </summary>
public sealed class HealthCheckPoller(
    IServiceScopeFactory scopeFactory,
    IHealthCheckExecutor executor,
    ILogger<HealthCheckPoller> logger,
    IHealthCheckNotifier? notifier = null) : BackgroundService
{
    private static readonly TimeSpan Tick = TimeSpan.FromSeconds(5);
    private readonly Dictionary<Guid, DateTimeOffset> _nextRun = new();
    // ponytail: per-process last status — a restart while unhealthy re-notifies once
    private readonly Dictionary<Guid, HealthStatus> _lastStatus = new();
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Tick);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                await TickOnce(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "health poll tick failed");
            }
        }
    }

    private async Task TickOnce(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IDevStationDb>();
        var now = DateTimeOffset.UtcNow;

        var due = (await db.HealthChecks.Where(h => h.Enabled).ToListAsync(ct))
            .Where(h => !_nextRun.TryGetValue(h.Id, out var next) || next <= now)
            .ToList();
        if (due.Count == 0) return;

        // run checks off the DbContext (not thread-safe) — persist + publish after
        var results = await Task.WhenAll(due.Select(async h =>
            (Check: h, Result: await executor.Execute(
                new HealthCheckDef(h.Id, h.Kind, h.Target, h.TimeoutSeconds), ct))));

        foreach (var (h, r) in results)
        {
            _nextRun[h.Id] = now + TimeSpan.FromSeconds(h.IntervalSeconds);
            db.HealthCheckResults.Add(new HealthCheckResult
            {
                HealthCheckId = h.Id,
                Status = r.Status,
                LatencyMs = r.LatencyMs,
                Error = r.Error,
                CheckedAt = r.CheckedAt,
            });
        }
        await db.SaveChangesAsync(ct);

        // broadcast only what actually persisted
        if (notifier is not null)
            foreach (var (h, r) in results)
                await notifier.Publish(new HealthCheckUpdate(h.Id, r.Status,
                    r.LatencyMs, r.Error, r.CheckedAt), ct);

        // health_unhealthy notification on transition into Unhealthy only
        var notifications = scope.ServiceProvider.GetService<INotificationService>();
        if (notifications is not null)
            foreach (var (h, r) in results)
            {
                var prev = _lastStatus.GetValueOrDefault(h.Id, HealthStatus.Unknown);
                _lastStatus[h.Id] = r.Status;
                if (prev == HealthStatus.Unhealthy || r.Status != HealthStatus.Unhealthy)
                    continue;
                try
                {
                    await notifications.NotifyAsync("health_unhealthy",
                        $"Health check {h.Name} unhealthy",
                        r.Error ?? h.Target, "error",
                        new
                        {
                            healthCheckId = h.Id,
                            h.Kind,
                            h.Target,
                            prev = prev.ToString().ToLowerInvariant()
                        }, ct);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "health_unhealthy notification failed");
                }
            }

        // retention: keep 7 days of results, one set-delete per tick
        var cutoff = now.AddDays(-7);
        await db.HealthCheckResults
            .Where(r => r.CheckedAt < cutoff)
            .ExecuteDeleteAsync(ct);
    }
}
