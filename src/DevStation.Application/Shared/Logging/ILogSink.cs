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

/// <summary>Write-side seam for the log pipeline. Task 11 replaces the
/// in-memory impl with LogBuffer + /hubs/logs streaming.</summary>
public interface ILogSink
{
    void Write(LogEntry e);
}

/// <summary>Optional read side so handlers can serve a resource's recent
/// lines from the buffer; sinks without history simply don't register it.</summary>
public interface ILogQuery
{
    IReadOnlyList<LogEntry> ForDeployment(Guid deploymentId);
}
