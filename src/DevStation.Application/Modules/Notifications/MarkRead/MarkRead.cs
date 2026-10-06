using DevStation.Application.Shared.Persistence;
using DevStation.Application.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace DevStation.Application.Modules.Notifications.MarkRead;

public sealed class MarkReadHandler(IDevStationDb db)
{
    /// <summary>POST /api/notifications/{id}/read — 404 when unknown.</summary>
    public async Task Handle(Guid id, CancellationToken ct)
    {
        var n = await db.Notifications.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new AppException("NOT_FOUND", 404, "Notification not found");
        if (!n.Read)
        {
            n.Read = true;
            await db.SaveChangesAsync(ct);
        }
    }

    /// <summary>POST /api/notifications/read-all — set-update, no rows loaded.</summary>
    public async Task<int> HandleAll(CancellationToken ct)
        => await db.Notifications.Where(n => !n.Read)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.Read, true), ct);
}
