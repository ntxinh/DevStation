using DevStation.Application.Shared.Persistence;
using DevStation.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DevStation.Application.Modules.Databases.ListQueryHistory;

public sealed record HistoryEntryDto(Guid Id, Guid? ConnectionId, string Sql,
    int? RowCount, int? DurationMs, bool Success, string? Error,
    DateTimeOffset ExecutedAt)
{
    public static HistoryEntryDto Of(QueryHistoryEntry h) => new(h.Id,
        h.ConnectionId, h.Sql, h.RowCount, h.DurationMs, h.Success, h.Error,
        h.ExecutedAt);
}

public sealed class ListQueryHistoryHandler(IDevStationDb db)
{
    public async Task<List<HistoryEntryDto>> Handle(Guid? connectionId,
        int limit, CancellationToken ct)
    {
        var q = db.QueryHistory.AsQueryable();
        if (connectionId is { } cid)
            q = q.Where(h => h.ConnectionId == cid);
        return (await q.OrderByDescending(h => h.ExecutedAt)
                .Take(Math.Clamp(limit, 1, 200)).ToListAsync(ct))
            .Select(HistoryEntryDto.Of).ToList();
    }
}
