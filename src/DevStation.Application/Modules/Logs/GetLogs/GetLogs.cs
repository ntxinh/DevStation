using System.Text.RegularExpressions;
using DevStation.Application.Shared.Logging;
using DevStation.Application.Shared.Persistence;
using DevStation.Application.Shared.Providers;
using DevStation.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DevStation.Application.Modules.Logs.GetLogs;

/// <summary>One log line as returned by GET /api/logs.</summary>
public sealed record LogLineDto(DateTimeOffset Ts, string Level, string Source,
    Guid? EnvironmentId, string Message, Dictionary<string, string>? Meta);

/// <summary>
/// Unified log history: the in-process buffer (api/deployment/job sources),
/// persisted deployment/job error lines, and — only when source=container —
/// on-demand tails from service-linked containers.
/// </summary>
public sealed partial class GetLogsHandler(IDevStationDb db, ILogQuery buffer,
    IContainerProvider containers)
{
    public async Task<IReadOnlyList<LogLineDto>> Handle(LogFilter filter,
        CancellationToken ct)
    {
        var take = Math.Clamp(filter.Take, 1, 500);
        if (string.Equals(filter.Source, "container",
                StringComparison.OrdinalIgnoreCase))
            return await ContainerLogs(filter, take, ct);

        var lines = buffer.Query(filter with { Take = take })
            .Select(ToDto).ToList();
        lines.AddRange(await PersistedErrors(filter, take, ct));

        IEnumerable<LogLineDto> merged = lines;
        if (filter.Q is { Length: > 0 } text)
            merged = merged.Where(l => l.Message.Contains(text,
                StringComparison.OrdinalIgnoreCase));
        if (filter.Severity is not null)
            merged = merged.Where(l => string.Equals(l.Level, filter.Severity,
                StringComparison.OrdinalIgnoreCase));
        return merged.OrderByDescending(l => l.Ts).Take(take).ToList();
    }

    private static LogLineDto ToDto(LogEntry e)
        => new(e.Ts, e.Level, e.Source, e.EnvironmentId, e.Message, e.Meta);

    /// <summary>Persisted failure lines from deployments/job executions —
    /// history that outlives the 10k ring. Emitted unless the caller
    /// filtered to a non-matching source.</summary>
    private async Task<List<LogLineDto>> PersistedErrors(LogFilter filter, int take,
        CancellationToken ct)
    {
        var rows = new List<LogLineDto>();
        if (Matches(filter.Source, "deployment"))
        {
            var deployments = await db.Deployments.AsNoTracking()
                .Where(d => d.Status == DeploymentStatus.Failed && d.Log != null)
                .Where(d => filter.EnvironmentId == null
                    || d.EnvironmentId == filter.EnvironmentId)
                .OrderByDescending(d => d.FinishedAt ?? d.CreatedAt)
                .Take(take)
                .Select(d => new { d.Id, d.EnvironmentId, d.Log, d.FinishedAt, d.CreatedAt })
                .ToListAsync(ct);
            foreach (var d in deployments)
                rows.Add(new LogLineDto(d.FinishedAt ?? d.CreatedAt, "error",
                    "deployment", d.EnvironmentId,
                    $"deployment {d.Id} failed — {Tail(d.Log!)}",
                    new() { ["deploymentId"] = d.Id.ToString() }));
        }
        if (Matches(filter.Source, "job"))
        {
            var execs = await db.JobExecutions.AsNoTracking()
                .Where(e => e.Error != null)
                .OrderByDescending(e => e.EndedAt ?? e.StartedAt)
                .Take(take)
                .Select(e => new { e.JobDefinitionId, e.Error, e.EndedAt, e.StartedAt })
                .ToListAsync(ct);
            foreach (var e in execs)
                rows.Add(new LogLineDto(e.EndedAt ?? e.StartedAt, "error",
                    "job", null, e.Error!,
                    new() { ["jobId"] = e.JobDefinitionId.ToString() }));
        }
        return rows;
    }

    private async Task<IReadOnlyList<LogLineDto>> ContainerLogs(LogFilter filter,
        int take, CancellationToken ct)
    {
        var services = await db.Services.AsNoTracking()
            .Where(s => s.ContainerId != null)
            .Where(s => filter.EnvironmentId == null
                || s.EnvironmentId == filter.EnvironmentId)
            .Select(s => new { s.Id, s.Name, s.ContainerId, s.EnvironmentId })
            .ToListAsync(ct);

        var rows = new List<LogLineDto>();
        foreach (var s in services)
        {
            IReadOnlyList<string> raw;
            try
            {
                // ponytail: per-container tail capped at 200; raise if a UI
                // pager ever needs deeper history
                raw = await containers.GetLogsAsync(s.ContainerId!, 200, ct);
            }
            catch (Exception) when (!ct.IsCancellationRequested)
            {
                continue; // gone container — skip, don't fail the request
            }
            foreach (var line in raw)
            {
                var ts = TryTs(line, out var msg);
                rows.Add(new LogLineDto(ts, "info", "container",
                    s.EnvironmentId, $"[{s.Name}] {msg}",
                    new() { ["serviceId"] = s.Id.ToString() }));
            }
        }

        IEnumerable<LogLineDto> q = rows;
        if (filter.Severity is not null)
            q = q.Where(l => string.Equals(l.Level, filter.Severity,
                StringComparison.OrdinalIgnoreCase));
        if (filter.Q is { Length: > 0 } text)
            q = q.Where(l => l.Message.Contains(text,
                StringComparison.OrdinalIgnoreCase));
        return q.OrderByDescending(l => l.Ts).Take(take).ToList();
    }

    /// <summary>Strip a leading RFC3339 timestamp written by the runtime.</summary>
    private static DateTimeOffset TryTs(string line, out string msg)
    {
        var m = TsPrefix().Match(line);
        if (m.Success && DateTimeOffset.TryParse(m.Groups[1].Value, out var ts))
        {
            msg = line[m.Length..];
            return ts;
        }
        msg = line;
        return DateTimeOffset.UtcNow;
    }

    private static bool Matches(string? source, string name)
        => source is null || string.Equals(source, name,
            StringComparison.OrdinalIgnoreCase);

    private static string Tail(string log)
        => log.Length <= 2000 ? log : log[^2000..];

    [GeneratedRegex(@"^(\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|[+-]\d{2}:?\d{2}))\s")]
    private static partial Regex TsPrefix();
}
