using System.Security.Claims;
using DevStation.Application.Shared.Auditing;
using DevStation.Application.Shared.Persistence;
using DevStation.Application.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace DevStation.Application.Modules.Health.DeleteHealthCheck;

public sealed class DeleteHealthCheckHandler(IDevStationDb db, IAuditWriter audit)
{
    public async Task Handle(Guid id, ClaimsPrincipal user, CancellationToken ct)
    {
        var h = await db.HealthChecks.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new AppException("NOT_FOUND", 404, "Health check not found");
        db.HealthChecks.Remove(h);
        db.HealthCheckResults.RemoveRange(
            db.HealthCheckResults.Where(r => r.HealthCheckId == id));
        await db.SaveChangesAsync(ct);
        await audit.Record("healthcheck.delete", "health_check", id,
            user.Identity?.Name, "ok", new { h.Name }, ct);
    }
}
