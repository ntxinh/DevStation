using DevStation.Application.Shared.Persistence;
using DevStation.Application.Shared.Providers;
using Microsoft.EntityFrameworkCore;

namespace DevStation.Application.Modules.Jobs.ListExecutions;

/// <summary>Most recent executions of one job (newest first, capped).</summary>
public sealed class ListExecutionsHandler(IDevStationDb db)
{
    public async Task<IReadOnlyList<JobExecutionInfo>> Handle(Guid jobId, CancellationToken ct)
        => await db.JobExecutions
            .Where(e => e.JobDefinitionId == jobId)
            .OrderByDescending(e => e.StartedAt)
            .Take(50)
            .Select(e => new JobExecutionInfo(e.Id, e.StartedAt, e.EndedAt,
                e.DurationMs, e.Status.ToString().ToLowerInvariant(), e.Error))
            .ToListAsync(ct);
}
