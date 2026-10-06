using DevStation.Application.Shared.Logging;
using Microsoft.Extensions.Logging;

namespace DevStation.Infrastructure.Logging;

/// <summary>
/// Bridges <see cref="ILogger"/> output into <see cref="ILogSink"/> with
/// source "api" so app logs stream over /hubs/logs without a Serilog dep.
/// Min level configurable via Logging:Buffered:MinLevel (default Information).
/// </summary>
public sealed class BufferedLoggerProvider(ILogSink sink,
    LogLevel minLevel = LogLevel.Information) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName)
        => new BufferedLogger(sink, minLevel);

    public void Dispose() { }

    private sealed class BufferedLogger(ILogSink sink, LogLevel minLevel) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel level)
            => level >= minLevel && level != LogLevel.None;

        public void Log<TState>(LogLevel level, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(level)) return;
            var message = formatter(state, exception);
            if (exception is not null)
                message += $" [{exception.GetType().Name}: {exception.Message}]";
            sink.Write(new LogEntry(DateTimeOffset.UtcNow,
                MapLevel(level), "api", null, message));
        }

        private static string MapLevel(LogLevel level) => level switch
        {
            LogLevel.Trace => "trace",
            LogLevel.Debug => "debug",
            LogLevel.Information => "info",
            LogLevel.Warning => "warn",
            LogLevel.Error => "error",
            _ => "fatal",
        };
    }
}
