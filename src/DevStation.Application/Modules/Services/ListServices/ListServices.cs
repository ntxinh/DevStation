using DevStation.Application.Shared.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DevStation.Application.Modules.Services.ListServices;

public sealed class ListServicesHandler(IDevStationDb db)
{
    public async Task<IReadOnlyList<ServiceDto>> Handle(Guid? environmentId,
        CancellationToken ct)
    {
        var q = db.Services.AsQueryable();
        if (environmentId is { } e) q = q.Where(s => s.EnvironmentId == e);
        var services = await q.OrderBy(s => s.Name).ToListAsync(ct);

        var envIds = services.Where(s => s.EnvironmentId != null)
            .Select(s => s.EnvironmentId!.Value).Distinct().ToList();
        var envNames = envIds.Count == 0 ? [] :
            await db.Environments.Where(x => envIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.Name, ct);

        return services.Select(s => ServiceDto.Of(s,
            s.EnvironmentId is { } id && envNames.TryGetValue(id, out var n) ? n : null))
            .ToList();
    }
}
