using DevStation.Application.Shared.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DevStation.Application.Modules.Environments.ListEnvironments;

public sealed class ListEnvironmentsHandler(IDevStationDb db)
{
    public async Task<List<EnvironmentDto>> Handle(CancellationToken ct)
    {
        var envs = await db.Environments
            .OrderBy(e => e.Order).ThenBy(e => e.Name)
            .ToListAsync(ct);
        var health = await EnvironmentsModule.LatestHealthByEnvironment(db, ct);
        return envs
            .Select(e => EnvironmentDto.Of(e, health.GetValueOrDefault(e.Id, "unknown")))
            .ToList();
    }
}
