using DevStation.Application.Shared.Providers;

namespace DevStation.Application.Modules.Jobs.ListJobs;

public sealed class ListJobsHandler(IJobProvider jobs)
{
    public Task<IReadOnlyList<JobInfo>> Handle(CancellationToken ct) => jobs.ListAsync(ct);
}
