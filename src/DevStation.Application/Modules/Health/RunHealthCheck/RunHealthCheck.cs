using DevStation.Application.Shared.Persistence;
using DevStation.Application.Shared.Providers;
using DevStation.Application.Shared.Results;
using DevStation.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DevStation.Application.Modules.Health.RunHealthCheck;

/// <summary>Manual run: execute now, persist the result, broadcast it.</summary>
public sealed class RunHealthCheckHandler(IDevStationDb db, IHealthCheckExecutor executor,
    IHealthCheckNotifier? notifier = null)
{
    public async Task<HealthResultDto> Handle(Guid id, CancellationToken ct)
    {
        var h = await db.HealthChecks.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new AppException("NOT_FOUND", 404, "Health check not found");

        var r = await executor.Execute(
            new HealthCheckDef(h.Id, h.Kind, h.Target, h.TimeoutSeconds), ct);
        db.HealthCheckResults.Add(new HealthCheckResult
        {
            HealthCheckId = h.Id,
            Status = r.Status,
            LatencyMs = r.LatencyMs,
            Error = r.Error,
            CheckedAt = r.CheckedAt,
        });
        await db.SaveChangesAsync(ct);
        if (notifier is not null)
            await notifier.Publish(new HealthCheckUpdate(h.Id, r.Status,
                r.LatencyMs, r.Error, r.CheckedAt), ct);
        return new HealthResultDto(r.Status.ToString().ToLowerInvariant(),
            r.LatencyMs, r.Error, r.CheckedAt);
    }
}
