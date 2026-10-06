using DevStation.Application.Shared.Providers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace DevStation.Web.Hubs;

[Authorize("Read")]
public sealed class JobsHub : Hub;

/// <summary>Bridges IJobNotifier → SignalR `job.updated` on /hubs/jobs.</summary>
public sealed class SignalRJobNotifier(IHubContext<JobsHub> hub) : IJobNotifier
{
    public Task Publish(JobStatusUpdate update, CancellationToken ct)
        => hub.Clients.All.SendAsync("job.updated", new
        {
            jobId = update.JobId,
            update.Name,
            update.Status,
            update.LastRun,
            update.NextRun,
            update.DurationMs,
        }, ct);
}
