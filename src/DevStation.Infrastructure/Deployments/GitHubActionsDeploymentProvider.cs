using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using DevStation.Application.Shared.Logging;
using DevStation.Application.Shared.Providers;
using DevStation.Application.Shared.Results;
using DevStation.Application.Shared.Secrets;
using DevStation.Domain.Entities;
using DevStation.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DevStation.Infrastructure.Deployments;

/// <summary>
/// GitHub Actions backend: workflow_dispatch then status polling.
/// Enabled via `Deployments:Provider=github` + `Deployments:GitHub`
/// {Owner, Repo, WorkflowId, Ref?}; token comes from ISecretStore
/// (`github.token`, i.e. DEVSTATION_SECRET_GITHUB_TOKEN).
/// </summary>
public sealed class GitHubActionsDeploymentProvider : IDeploymentProvider
{
    private sealed class Options
    {
        public string Owner { get; set; } = "";
        public string Repo { get; set; } = "";
        public string WorkflowId { get; set; } = "";
        public string Ref { get; set; } = "main";
        public double PollSeconds { get; set; } = 5;
    }

    private readonly Options _o = new();
    private readonly ISecretStore _secrets;
    private readonly IHttpClientFactory _http;
    private readonly DeploymentStore _store;
    private readonly ConcurrentDictionary<Guid, long> _runIds = new();
    private readonly ILogger? _logger;

    public GitHubActionsDeploymentProvider(IServiceScopeFactory scopeFactory,
        ISecretStore secrets, IHttpClientFactory http, IConfiguration config,
        ILogSink sink, IDeploymentNotifier? notifier = null,
        ILogger<GitHubActionsDeploymentProvider>? logger = null)
    {
        _secrets = secrets;
        _http = http;
        _logger = logger;
        config.GetSection("Deployments:GitHub").Bind(_o);
        _store = new DeploymentStore(scopeFactory, sink, notifier, logger);
    }

    public async Task<Guid> StartAsync(Guid? projectId, Guid? environmentId,
        string? commit, string? triggeredBy, CancellationToken ct)
    {
        var d = new Deployment
        {
            ProjectId = projectId, EnvironmentId = environmentId,
            Commit = commit ?? _o.Ref, Status = DeploymentStatus.Queued,
            TriggeredBy = triggeredBy,
        };
        _store.LogLine(d, "info", $"dispatching {_o.WorkflowId} on {_o.Owner}/{_o.Repo}");
        await _store.Persist(d, ct);
        await _store.Notify(d, ct);
        _ = Task.Run(() => Drive(d, commit ?? _o.Ref), CancellationToken.None);
        return d.Id;
    }

    public async Task CancelAsync(Guid id, CancellationToken ct)
    {
        var row = await _store.Find(id, ct)
            ?? throw new AppException("NOT_FOUND", 404, "Deployment not found");
        if (row.Status is not (DeploymentStatus.Queued or DeploymentStatus.Running))
            throw new AppException("CONFLICT", 409,
                $"Deployment already {row.Status.ToString().ToLowerInvariant()}");

        if (_runIds.TryGetValue(id, out var runId))
        {
            var client = await Client(ct);
            var res = await client.PostAsync(
                $"repos/{_o.Owner}/{_o.Repo}/actions/runs/{runId}/cancel",
                null, ct);
            res.EnsureSuccessStatusCode();
        }
        await _store.Mutate(id, DeploymentStatus.Cancelled, ct);
        _store.LogLine(row, "info", "deployment cancelled by user");
    }

    public async Task<DeploymentStatusInfo> GetStatusAsync(Guid id, CancellationToken ct)
    {
        var row = await _store.Find(id, ct)
            ?? throw new AppException("NOT_FOUND", 404, "Deployment not found");
        return new DeploymentStatusInfo(row.Status,
            row.Log?.Split('\n', StringSplitOptions.RemoveEmptyEntries) ?? [],
            row.StartedAt, row.FinishedAt, row.Commit, row.Version);
    }

    private async Task<HttpClient> Client(CancellationToken ct)
    {
        var token = await _secrets.GetAsync("github.token", ct)
            ?? throw new AppException("PROVIDER_ERROR", 502,
                "github.token not configured in secret store");
        var c = _http.CreateClient();
        c.BaseAddress = new Uri("https://api.github.com/");
        c.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        c.DefaultRequestHeaders.UserAgent.ParseAdd("DevStation");
        c.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return c;
    }

    private async Task Drive(Deployment d, string gitRef)
    {
        try
        {
            var client = await Client(CancellationToken.None);
            var res = await client.PostAsJsonAsync(
                $"repos/{_o.Owner}/{_o.Repo}/actions/workflows/{_o.WorkflowId}/dispatches",
                new { @ref = gitRef }, CancellationToken.None);
            res.EnsureSuccessStatusCode();
            await Set(d, DeploymentStatus.Running, "workflow_dispatch accepted");

            // GitHub needs a beat before the run shows up in the list
            var deadline = DateTimeOffset.UtcNow.AddMinutes(30);
            while (DateTimeOffset.UtcNow < deadline)
            {
                await Task.Delay(TimeSpan.FromSeconds(_o.PollSeconds));
                var run = await LatestRun(client);
                if (run is null) continue;
                _runIds[d.Id] = run.Id;
                d.ProviderDeploymentId = run.Id.ToString();
                var status = MapStatus(run);
                if (status is DeploymentStatus.Queued or DeploymentStatus.Running)
                    continue;
                await Set(d, status, $"run {run.Id} → {run.Status}/{run.Conclusion}");
                return;
            }
            await Set(d, DeploymentStatus.Failed, "polling timed out after 30m");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger?.LogWarning(ex, "github deployment {Id} failed", d.Id);
            await Set(d, DeploymentStatus.Failed, ex.Message);
        }
    }

    private async Task Set(Deployment d, DeploymentStatus status, string line)
    {
        if (status == DeploymentStatus.Running) d.StartedAt ??= DateTimeOffset.UtcNow;
        if (status is not (DeploymentStatus.Queued or DeploymentStatus.Running))
            d.FinishedAt = DateTimeOffset.UtcNow;
        // honour user cancellation — never overwrite a terminal state
        var fresh = await _store.Find(d.Id, CancellationToken.None);
        if (fresh is not null &&
            fresh.Status is not (DeploymentStatus.Queued or DeploymentStatus.Running))
            return;
        d.Status = status;
        _store.LogLine(d, "info", line);
        await _store.Persist(d, CancellationToken.None);
        await _store.Notify(d, CancellationToken.None);
    }

    private static DeploymentStatus MapStatus(GhRun run) =>
        (run.Status, run.Conclusion) switch
        {
            ("completed", "success") => DeploymentStatus.Succeeded,
            ("completed", "cancelled") => DeploymentStatus.Cancelled,
            ("completed", _) => DeploymentStatus.Failed,
            (_, _) => DeploymentStatus.Running,
        };

    private async Task<GhRun?> LatestRun(HttpClient client)
    {
        var res = await client.GetFromJsonAsync<GhRunList>(
            $"repos/{_o.Owner}/{_o.Repo}/actions/workflows/{_o.WorkflowId}/runs?per_page=1&event=workflow_dispatch",
            CancellationToken.None);
        return res?.Runs.FirstOrDefault();
    }

    private sealed record GhRun(
        [property: JsonPropertyName("id")] long Id,
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("conclusion")] string? Conclusion);

    private sealed record GhRunList(
        [property: JsonPropertyName("workflow_runs")] List<GhRun> Runs);
}
