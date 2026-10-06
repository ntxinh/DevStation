using System.Security.Claims;
using DevStation.Application.Shared.Auditing;
using DevStation.Application.Shared.Providers;
using DevStation.Application.Shared.Results;

namespace DevStation.Application.Modules.Jobs.TriggerJob;

/// <summary>Queue an immediate run; the job itself executes asynchronously
/// (the listener records the JobExecution + broadcasts when it finishes).</summary>
public sealed class TriggerJobHandler(IJobProvider jobs, IAuditWriter audit)
{
    public async Task<JobInfo> Handle(Guid id, ClaimsPrincipal user, CancellationToken ct)
    {
        await jobs.TriggerAsync(id, ct);
        await audit.Record("job.trigger", "job", id, user.Identity?.Name, "ok", null, ct);
        return await jobs.GetAsync(id, ct)
            ?? throw new AppException("NOT_FOUND", 404, "Job not found");
    }
}
