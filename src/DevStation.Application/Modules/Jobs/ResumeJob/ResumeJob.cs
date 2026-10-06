using System.Security.Claims;
using DevStation.Application.Shared.Auditing;
using DevStation.Application.Shared.Providers;
using DevStation.Application.Shared.Results;

namespace DevStation.Application.Modules.Jobs.ResumeJob;

public sealed class ResumeJobHandler(IJobProvider jobs, IAuditWriter audit)
{
    public async Task<JobInfo> Handle(Guid id, ClaimsPrincipal user, CancellationToken ct)
    {
        await jobs.ResumeAsync(id, ct);
        await audit.Record("job.resume", "job", id, user.Identity?.Name, "ok", null, ct);
        return await jobs.GetAsync(id, ct)
            ?? throw new AppException("NOT_FOUND", 404, "Job not found");
    }
}
