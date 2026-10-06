using System.Security.Claims;
using DevStation.Application.Shared.Auditing;
using DevStation.Application.Shared.Persistence;
using DevStation.Application.Shared.Results;
using DevStation.Application.Shared.Secrets;
using Microsoft.EntityFrameworkCore;

namespace DevStation.Application.Modules.Databases.DeleteConnection;

public sealed class DeleteConnectionHandler(IDevStationDb db, ISecretStore secrets,
    IAuditWriter audit)
{
    public async Task Handle(Guid id, ClaimsPrincipal user, CancellationToken ct)
    {
        var c = await db.DatabaseConnections.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new AppException("NOT_FOUND", 404, "Connection not found");

        // no FK cascades — remove conn-owned rows or they orphan
        db.QueryHistory.RemoveRange(db.QueryHistory.Where(h => h.ConnectionId == id));
        db.SavedQueries.RemoveRange(db.SavedQueries.Where(q => q.ConnectionId == id));
        db.DatabaseConnections.Remove(c);
        if (c.PasswordRef is { } r)
            await secrets.DeleteAsync(r.Key, ct);
        await db.SaveChangesAsync(ct);
        await audit.Record("dbconnection.delete", "database_connection", id,
            user.Identity?.Name, "ok", new { c.Name }, ct);
    }
}
