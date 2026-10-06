using System.Security.Claims;
using DevStation.Application.Shared.Auditing;
using DevStation.Application.Shared.Persistence;
using DevStation.Application.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace DevStation.Application.Modules.Environments.DeleteEnvironment;

public sealed class DeleteEnvironmentHandler(IDevStationDb db, IAuditWriter audit)
{
    public async Task Handle(Guid id, ClaimsPrincipal user, CancellationToken ct)
    {
        var e = await db.Environments.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new AppException("NOT_FOUND", 404, "Environment not found");
        db.Environments.Remove(e);
        await db.SaveChangesAsync(ct);
        await audit.Record("environment.delete", "environment", id,
            user.Identity?.Name, "ok", new { e.Name }, ct);
    }
}
