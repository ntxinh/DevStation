using System.Security.Claims;
using DevStation.Application.Shared.Auditing;
using DevStation.Application.Shared.Persistence;
using DevStation.Application.Shared.Providers;
using DevStation.Application.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace DevStation.Application.Modules.Services.RestartService;

/// <summary>Restart is only possible when the service maps to a runtime container;
/// otherwise there is no process to control → SERVICE_NOT_RESTARTABLE.
/// After the restart completes, re-checks health (same probe as GET health) and
/// persists the fresh status.</summary>
public sealed class RestartServiceHandler(IDevStationDb db,
    IContainerProvider containers, IHealthCheckExecutor executor, IAuditWriter audit)
{
    public async Task<ServiceDto> Handle(Guid id, ClaimsPrincipal user,
        CancellationToken ct)
    {
        var s = await db.Services.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new AppException("NOT_FOUND", 404, "Service not found");
        if (string.IsNullOrWhiteSpace(s.ContainerId))
            throw new AppException("SERVICE_NOT_RESTARTABLE", 422,
                "Service has no container to restart");

        await containers.RestartAsync(s.ContainerId, ct);

        string? health = null;
        if (!string.IsNullOrWhiteSpace(s.Url))
        {
            var r = await ServiceHealthCheck.Recheck(db, executor, s, ct);
            health = r.Status.ToString().ToLowerInvariant();
        }
        else
        {
            s.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        await audit.Record("service.restart", "service", s.Id,
            user.Identity?.Name, "ok",
            new { s.ContainerId, health }, ct);
        return ServiceDto.Of(s);
    }
}
