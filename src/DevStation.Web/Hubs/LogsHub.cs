using DevStation.Application.Shared.Logging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace DevStation.Web.Hubs;

[Authorize("Read")]
public sealed class LogsHub : Hub;

/// <summary>Bridges ILogNotifier → SignalR `log.entry` on /hubs/logs.</summary>
public sealed class SignalRLogNotifier(IHubContext<LogsHub> hub) : ILogNotifier
{
    public Task Publish(LogEntry e, CancellationToken ct = default)
        => hub.Clients.All.SendAsync("log.entry", new
        {
            ts = e.Ts,
            level = e.Level,
            source = e.Source,
            environmentId = e.EnvironmentId,
            message = e.Message,
            meta = e.Meta,
        }, ct);
}
