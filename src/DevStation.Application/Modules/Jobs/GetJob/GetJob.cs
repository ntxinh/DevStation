using DevStation.Application.Modules.Jobs.ListExecutions;
using DevStation.Application.Shared.Providers;
using DevStation.Application.Shared.Results;

namespace DevStation.Application.Modules.Jobs.GetJob;

public sealed record JobDetail(JobInfo Job, IReadOnlyList<JobExecutionInfo> Executions);

public sealed class GetJobHandler(IJobProvider jobs, ListExecutionsHandler executions)
{
    public async Task<JobDetail> Handle(Guid id, CancellationToken ct)
    {
        var job = await jobs.GetAsync(id, ct)
            ?? throw new AppException("NOT_FOUND", 404, "Job not found");
        return new JobDetail(job, await executions.Handle(id, ct));
    }
}
