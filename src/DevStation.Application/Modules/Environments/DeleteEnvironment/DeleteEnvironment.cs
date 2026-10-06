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

        // no FK cascade in schema — remove rows owned by this env or they orphan
        // (an orphan enabled check would keep polling forever).
        // Deployments keep their envId for history.
        var checks = await db.HealthChecks
            .Where(h => h.EnvironmentId == id).ToListAsync(ct);
        var checkIds = checks.Select(h => h.Id).ToList();
        db.HealthCheckResults.RemoveRange(
            db.HealthCheckResults.Where(r => checkIds.Contains(r.HealthCheckId)));
        db.HealthChecks.RemoveRange(checks);
        db.Services.RemoveRange(db.Services.Where(s => s.EnvironmentId == id));

        db.Environments.Remove(e);
        await db.SaveChangesAsync(ct);
        await audit.Record("environment.delete", "environment", id,
            user.Identity?.Name, "ok",
            new { e.Name, checksRemoved = checks.Count }, ct);
    }
}
