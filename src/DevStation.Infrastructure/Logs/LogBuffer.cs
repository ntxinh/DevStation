using System.Collections.Concurrent;
using System.Threading.Channels;
using DevStation.Application.Shared.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace DevStation.Infrastructure.Logs;

/// <summary>
/// Bounded in-process log store: an unbounded channel takes every write and
/// feeds hub broadcast, while a 10k ring (drop-oldest) backs queries.
/// Replaces the interim InMemoryLogSink — same ILogSink/ILogQuery seams.
/// </summary>
/// <remarks>
/// The notifier is resolved lazily on a thread-pool continuation: LogBuffer
/// sits under ILoggerProvider, which is built while the DI container is still
/// constructing — pulling IHubContext there deadlocks the host.
/// </remarks>
public sealed class LogBuffer : ILogSink, ILogQuery
{
    public const int DefaultCap = 10_000;

    private readonly int _cap;
    private readonly ConcurrentQueue<LogEntry> _entries = new();
    private readonly Channel<LogEntry> _broadcast =
        Channel.CreateUnbounded<LogEntry>();
    // ConcurrentQueue.Count snapshots segment locks — O(segments) per call,
    // too hot for Write; track the size ourselves
    private int _count;

    public int Count => Volatile.Read(ref _count);

    public LogBuffer(IServiceProvider? services = null, int cap = DefaultCap)
    {
        _cap = cap;
        if (services is not null) _ = PumpAsync(services);
    }

    public void Write(LogEntry e)
    {
        _entries.Enqueue(e);
        Interlocked.Increment(ref _count);
        while (Volatile.Read(ref _count) > _cap && _entries.TryDequeue(out _))
            Interlocked.Decrement(ref _count);
        _broadcast.Writer.TryWrite(e);
    }

    private async Task PumpAsync(IServiceProvider services)
    {
        await Task.Yield(); // off the DI construction call stack
        ILogNotifier? notifier;
        try { notifier = services.GetService<ILogNotifier>(); }
        catch { notifier = null; }
        if (notifier is null) return;

        await foreach (var e in _broadcast.Reader.ReadAllAsync())
        {
            try { await notifier.Publish(e); }
            catch { /* hub publish is best-effort — never stall writers */ }
        }
    }

    public IReadOnlyList<LogEntry> ForDeployment(Guid deploymentId)
        => _entries.Where(e => e.Meta is not null &&
            e.Meta.TryGetValue("deploymentId", out var v) &&
            Guid.TryParse(v, out var id) && id == deploymentId)
            .OrderBy(e => e.Ts).ToList();

    public IReadOnlyList<LogEntry> Query(LogFilter filter)
    {
        IEnumerable<LogEntry> q = _entries.OrderByDescending(e => e.Ts);
        if (filter.Source is not null)
            q = q.Where(e => string.Equals(e.Source, filter.Source,
                StringComparison.OrdinalIgnoreCase));
        if (filter.Severity is not null)
            q = q.Where(e => string.Equals(e.Level, filter.Severity,
                StringComparison.OrdinalIgnoreCase));
        if (filter.EnvironmentId is not null)
            q = q.Where(e => e.EnvironmentId == filter.EnvironmentId);
        if (filter.Q is { Length: > 0 } text)
            q = q.Where(e => e.Message.Contains(text,
                StringComparison.OrdinalIgnoreCase));
        var take = Math.Clamp(filter.Take, 1, 500);
        return q.Take(take).ToList();
    }
}
