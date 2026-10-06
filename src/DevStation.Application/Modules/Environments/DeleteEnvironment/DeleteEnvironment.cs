using System.Security.Claims;
using DevStation.Application.Shared.Auditing;
using DevStation.Application.Shared.Persistence;
using DevStation.Application.Shared.Results;
using DevStation.Application.Shared.Secrets;
using Microsoft.EntityFrameworkCore;

namespace DevStation.Application.Modules.Environments.DeleteEnvironment;

public sealed class DeleteEnvironmentHandler(IDevStationDb db, ISecretStore secrets,
    IAuditWriter audit)
{
    public async Task Handle(Guid id, ClaimsPrincipal user, CancellationToken ct)
    {
        var e = await db.Environments.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new AppException("NOT_FOUND", 404, "Environment not found");

        // no FK cascade in schema — remove rows owned by this env or they orphan
        // (an orphan enabled check would keep polling forever). Checks can hang
        // off the env or one of its services. Deployments keep envId for history.
        var serviceIds = await db.Services.Where(s => s.EnvironmentId == id)
            .Select(s => s.Id).ToListAsync(ct);
        var checks = await db.HealthChecks
            .Where(h => h.EnvironmentId == id
                || (h.ServiceId != null && serviceIds.Contains(h.ServiceId.Value)))
            .ToListAsync(ct);
        var checkIds = checks.Select(h => h.Id).ToList();
        db.HealthCheckResults.RemoveRange(
            db.HealthCheckResults.Where(r => checkIds.Contains(r.HealthCheckId)));
        db.HealthChecks.RemoveRange(checks);
        db.Services.RemoveRange(db.Services.Where(s => s.EnvironmentId == id));

        // env-linked connections would lose SQL_PROD_WRITE_BLOCKED if their
        // envId dangled — delete them and their conn-owned rows, then tombstone
        // their stored passwords (same order as DeleteConnection).
        var conns = await db.DatabaseConnections
            .Where(c => c.EnvironmentId == id).ToListAsync(ct);
        var connIds = conns.Select(c => c.Id).ToList();
        db.QueryHistory.RemoveRange(
            db.QueryHistory.Where(h => h.ConnectionId != null
                && connIds.Contains(h.ConnectionId.Value)));
        db.SavedQueries.RemoveRange(
            db.SavedQueries.Where(q => q.ConnectionId != null
                && connIds.Contains(q.ConnectionId.Value)));
        db.DatabaseConnections.RemoveRange(conns);

        foreach (var c in conns)
            if (c.PasswordRef is { } r)
                await secrets.DeleteAsync(r.Key, ct);

        db.Environments.Remove(e);
        await db.SaveChangesAsync(ct);
        await audit.Record("environment.delete", "environment", id,
            user.Identity?.Name, "ok",
            new { e.Name, checksRemoved = checks.Count }, ct);
    }
}
