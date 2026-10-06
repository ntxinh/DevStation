using System.Collections.Concurrent;
using DevStation.Application.Shared.Logging;
using DevStation.Application.Shared.Providers;
using DevStation.Application.Shared.Results;
using DevStation.Domain.Entities;
using DevStation.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DevStation.Infrastructure.Deployments;

public sealed record MockDeploymentOptions
{
    /// <summary>Queued → Running delay, seconds.</summary>
    public double QueueSeconds { get; init; } = 2;
    /// <summary>Running → terminal delay, seconds.</summary>
    public double RunSeconds { get; init; } = 5;
    /// <summary>Probability a run ends Failed instead of Succeeded.</summary>
    public double FailRate { get; init; }
}

/// <summary>
/// Default provider: in-memory state machine
/// queued → (QueueSeconds) running → (RunSeconds) succeeded|failed, dual-
/// written to the deployments table. Emits `deployment.updated` + synthetic
/// log lines via ILogSink. No BackgroundService — each run drives itself.
/// </summary>
public sealed class MockDeploymentProvider : IDeploymentProvider
{
    private sealed record Run(Deployment Entity, List<string> Lines,
        CancellationTokenSource Cts)
    {
        public object Gate { get; } = new();
    }

    private readonly ConcurrentDictionary<Guid, Run> _runs = new();
    private readonly DeploymentStore _store;
    private readonly MockDeploymentOptions _options;
    private readonly ILogger? _logger;

    public MockDeploymentProvider(IServiceScopeFactory? scopeFactory,
        ILogSink sink, IDeploymentNotifier? notifier = null,
        MockDeploymentOptions? options = null,
        ILogger<MockDeploymentProvider>? logger = null)
    {
        _options = options ?? new MockDeploymentOptions();
        _logger = logger;
        _store = new DeploymentStore(scopeFactory, sink, notifier, logger);
    }

    public async Task<Guid> StartAsync(Guid? projectId, Guid? environmentId,
        string? commit, string? triggeredBy, CancellationToken ct)
    {
        var run = new Run(
            new Deployment
            {
                ProjectId = projectId,
                EnvironmentId = environmentId,
                Commit = commit,
                Status = DeploymentStatus.Queued,
                TriggeredBy = triggeredBy,
            },
            [], new CancellationTokenSource());
        _runs[run.Entity.Id] = run;

        Log(run, $"deployment queued{(commit is null ? "" : $" @ {commit}")}");
        await _store.Persist(run.Entity, ct);
        await _store.Notify(run.Entity, ct);

        // drive progression on its own token — the request ct dies with the request
        _ = Task.Run(() => Drive(run), CancellationToken.None);
        return run.Entity.Id;
    }

    public async Task CancelAsync(Guid id, CancellationToken ct)
    {
        if (_runs.TryGetValue(id, out var run))
        {
            bool cancelled;
            lock (run.Gate)
            {
                cancelled = run.Entity.Status is DeploymentStatus.Queued
                    or DeploymentStatus.Running;
                if (cancelled)
                {
                    run.Entity.Status = DeploymentStatus.Cancelled;
                    run.Entity.FinishedAt = DateTimeOffset.UtcNow;
                    run.Cts.Cancel();
                }
            }
            if (!cancelled)
                throw new AppException("CONFLICT", 409,
                    $"Deployment already {run.Entity.Status.ToString().ToLowerInvariant()}");
            Log(run, "deployment cancelled by user");
            await _store.Persist(run.Entity, ct);
            await _store.Notify(run.Entity, ct);
            return;
        }

        // not in memory (restarted process / other provider's row)
        var row = await _store.Find(id, ct)
            ?? throw new AppException("NOT_FOUND", 404, "Deployment not found");
        if (row.Status is not (DeploymentStatus.Queued or DeploymentStatus.Running))
            throw new AppException("CONFLICT", 409,
                $"Deployment already {row.Status.ToString().ToLowerInvariant()}");
        await _store.Mutate(id, DeploymentStatus.Cancelled, ct);
    }

    public async Task<DeploymentStatusInfo> GetStatusAsync(Guid id, CancellationToken ct)
    {
        if (_runs.TryGetValue(id, out var run))
        {
            List<string> lines;
            lock (run.Gate) lines = [.. run.Lines];
            return new DeploymentStatusInfo(run.Entity.Status, lines,
                run.Entity.StartedAt, run.Entity.FinishedAt,
                run.Entity.Commit, run.Entity.Version);
        }
        var row = await _store.Find(id, ct)
            ?? throw new AppException("NOT_FOUND", 404, "Deployment not found");
        return new DeploymentStatusInfo(row.Status,
            row.Log?.Split('\n', StringSplitOptions.RemoveEmptyEntries) ?? [],
            row.StartedAt, row.FinishedAt, row.Commit, row.Version);
    }

    private async Task Drive(Run run)
    {
        var token = run.Cts.Token;
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(_options.QueueSeconds), token);
            if (!Transition(run, DeploymentStatus.Running)) return;
            await _store.Persist(run.Entity, CancellationToken.None);
            await _store.Notify(run.Entity, CancellationToken.None);

            var fail = Random.Shared.NextDouble() < _options.FailRate;
            await Task.Delay(TimeSpan.FromSeconds(_options.RunSeconds), token);
            if (!Transition(run, fail ? DeploymentStatus.Failed : DeploymentStatus.Succeeded))
                return;
            await _store.Persist(run.Entity, CancellationToken.None);
            await _store.Notify(run.Entity, CancellationToken.None);
        }
        catch (OperationCanceledException) { /* cancel already recorded + notified */ }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "mock deployment {Id} driver failed", run.Entity.Id);
        }
    }

    /// <summary>CAS-style transition: no-op if the run was cancelled meanwhile.</summary>
    private bool Transition(Run run, DeploymentStatus status)
    {
        var d = run.Entity;
        lock (run.Gate)
        {
            if (d.Status is not (DeploymentStatus.Queued or DeploymentStatus.Running))
                return false;
            d.Status = status;
            if (status == DeploymentStatus.Running)
            {
                d.StartedAt = DateTimeOffset.UtcNow;
                Log(run, "deployment started");
            }
            else
            {
                d.FinishedAt = DateTimeOffset.UtcNow;
                Log(run, status == DeploymentStatus.Succeeded
                    ? "deployment succeeded"
                    : "deployment failed");
            }
        }
        return true;
    }

    private void Log(Run run, string message)
    {
        lock (run.Gate) run.Lines.Add(message);
        _store.LogLine(run.Entity, "info", message);
    }
}
