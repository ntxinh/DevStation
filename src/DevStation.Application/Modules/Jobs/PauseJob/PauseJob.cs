using System.Security.Claims;
using DevStation.Application.Shared.Auditing;
using DevStation.Application.Shared.Providers;
using DevStation.Application.Shared.Results;

namespace DevStation.Application.Modules.Jobs.PauseJob;

public sealed class PauseJobHandler(IJobProvider jobs, IAuditWriter audit)
{
    public async Task<JobInfo> Handle(Guid id, ClaimsPrincipal user, CancellationToken ct)
    {
        await jobs.PauseAsync(id, ct);
        await audit.Record("job.pause", "job", id, user.Identity?.Name, "ok", null, ct);
        return await jobs.GetAsync(id, ct)
            ?? throw new AppException("NOT_FOUND", 404, "Job not found");
    }
}
