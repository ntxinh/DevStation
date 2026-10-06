using System.Security.Claims;
using DevStation.Application.Shared.Auditing;
using DevStation.Application.Shared.Persistence;
using DevStation.Application.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace DevStation.Application.Modules.Databases.DeleteSavedQuery;

public sealed class DeleteSavedQueryHandler(IDevStationDb db, IAuditWriter audit)
{
    public async Task Handle(Guid id, ClaimsPrincipal user, CancellationToken ct)
    {
        var q = await db.SavedQueries.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new AppException("NOT_FOUND", 404, "Saved query not found");
        db.SavedQueries.Remove(q);
        await db.SaveChangesAsync(ct);
        await audit.Record("savedquery.delete", "saved_query", id,
            user.Identity?.Name, "ok", new { q.Name }, ct);
    }
}
