using DevStation.Application.Shared.Logging;
using DevStation.Application.Shared.Persistence;
using DevStation.Application.Shared.Providers;
using DevStation.Domain.Entities;
using DevStation.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DevStation.Infrastructure.Deployments;

/// <summary>
/// Shared persistence + log-sink + notify plumbing for deployment providers.
/// Each write gets its own scope: providers are singletons, the context is scoped.
/// </summary>
internal sealed class DeploymentStore(IServiceScopeFactory? scopeFactory,
    ILogSink sink, IDeploymentNotifier? notifier,
    ILogger? logger = null)
{
    public void LogLine(Deployment run, string level, string message) =>
        sink.Write(new LogEntry(DateTimeOffset.UtcNow, level, "deployment",
            run.EnvironmentId, message,
            new Dictionary<string, string> { ["deploymentId"] = run.Id.ToString() }));

    /// <summary>Insert-or-update the deployments row for this run.</summary>
    public async Task Persist(Deployment run, CancellationToken ct)
    {
        if (scopeFactory is null) return;
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetService<IDevStationDb>();
            if (db is null) return;
            if (await db.Deployments.AnyAsync(d => d.Id == run.Id, ct))
                db.Deployments.Update(run);
            else
                db.Deployments.Add(run);
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger?.LogWarning(ex, "deployment {Id} persist failed", run.Id);
        }
    }

    /// <summary>Load a deployments row when the provider has no memory of it
    /// (e.g. after restart). Returns null without a scope factory.</summary>
    public async Task<Deployment?> Find(Guid id, CancellationToken ct)
    {
        if (scopeFactory is null) return null;
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetService<IDevStationDb>();
        if (db is null) return null;
        return await db.Deployments.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id, ct);
    }

    /// <summary>Apply a terminal mutation to the persisted row (deployments
    /// created before this process, or rows written by another provider).</summary>
    public async Task<bool> Mutate(Guid id, DeploymentStatus status,
        CancellationToken ct)
    {
        if (scopeFactory is null) return false;
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetService<IDevStationDb>();
        if (db is null) return false;
        var row = await db.Deployments.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (row is null) return false;
        row.Status = status;
        row.FinishedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task Notify(Deployment run, CancellationToken ct)
    {
        if (notifier is null) return;
        try
        {
            await notifier.Publish(new DeploymentUpdate(run.Id, run.Status,
                run.StartedAt, run.FinishedAt), ct);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "deployment.updated publish failed");
        }
    }
}
