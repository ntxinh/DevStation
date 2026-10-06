using DevStation.Application.Shared.Logging;

namespace DevStation.Infrastructure.Logging;

/// <summary>
/// Temporary sink: keeps recent entries in memory (10k cap) and exposes a
/// per-deployment query. Task 11 replaces this with LogBuffer + /hubs/logs —
/// only the DI registration changes.
/// </summary>
public sealed class InMemoryLogSink : ILogSink, ILogQuery
{
    private const int Cap = 10_000;
    private readonly Queue<LogEntry> _entries = new();
    private readonly object _gate = new();

    public void Write(LogEntry e)
    {
        lock (_gate)
        {
            _entries.Enqueue(e);
            while (_entries.Count > Cap) _entries.Dequeue();
        }
    }

    public IReadOnlyList<LogEntry> ForDeployment(Guid deploymentId)
    {
        lock (_gate)
            return _entries
                .Where(e => e.Meta is not null &&
                    e.Meta.TryGetValue("deploymentId", out var v) &&
                    Guid.TryParse(v, out var id) && id == deploymentId)
                .ToList();
    }
}
