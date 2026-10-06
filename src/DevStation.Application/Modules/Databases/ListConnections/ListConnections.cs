using DevStation.Application.Shared.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DevStation.Application.Modules.Databases.ListConnections;

public sealed class ListConnectionsHandler(IDevStationDb db)
{
    public async Task<List<ConnectionDto>> Handle(CancellationToken ct)
    {
        var conns = await db.DatabaseConnections
            .OrderBy(c => c.Name).ToListAsync(ct);
        var envTypes = await db.Environments
            .Where(e => conns.Select(c => c.EnvironmentId).Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, e => e.Type, ct);
        return conns
            .Select(c => ConnectionDto.Of(c,
                c.EnvironmentId is { } id ? envTypes.GetValueOrDefault(id) : null))
            .ToList();
    }
}
