namespace DevStation.Application.Shared.Logging;

/// <summary>One buffered log line. <c>Meta</c> carries correlation keys
/// (e.g. <c>deploymentId</c>) for per-resource queries.</summary>
public sealed record LogEntry(
    DateTimeOffset Ts,
    string Level,
    string Source,
    Guid? EnvironmentId,
    string Message,
    Dictionary<string, string>? Meta = null);

/// <summary>Write-side seam for the log pipeline. Implemented by the
/// bounded LogBuffer which also broadcasts to /hubs/logs.</summary>
public interface ILogSink
{
    void Write(LogEntry e);
}

/// <summary>Filters applied over buffered entries (all optional).</summary>
public sealed record LogFilter(string? Source = null, string? Severity = null,
    Guid? EnvironmentId = null, string? Q = null, int Take = 100);

/// <summary>Optional live-broadcast seam; the web layer bridges it to
/// /hubs/logs so sink writes stream to connected clients.</summary>
public interface ILogNotifier
{
    Task Publish(LogEntry e, CancellationToken ct = default);
}

/// <summary>Optional read side so handlers can serve a resource's recent
/// lines from the buffer; sinks without history simply don't register it.</summary>
public interface ILogQuery
{
    IReadOnlyList<LogEntry> ForDeployment(Guid deploymentId);
    /// <summary>Newest-first filtered snapshot of the buffer.</summary>
    IReadOnlyList<LogEntry> Query(LogFilter filter);
}
