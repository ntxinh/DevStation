using DevStation.Application.Modules.Databases.SaveQuery;
using DevStation.Application.Shared.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DevStation.Application.Modules.Databases.ListSavedQueries;

public sealed class ListSavedQueriesHandler(IDevStationDb db)
{
    public async Task<List<SavedQueryDto>> Handle(Guid? connectionId,
        CancellationToken ct)
    {
        var q = db.SavedQueries.AsQueryable();
        if (connectionId is { } cid)
            q = q.Where(s => s.ConnectionId == cid);
        return (await q.OrderBy(s => s.Name).ToListAsync(ct))
            .Select(SavedQueryDto.Of).ToList();
    }
}
