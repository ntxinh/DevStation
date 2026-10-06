using DevStation.Application.Modules.Health;
using DevStation.Application.Shared.Auditing;
using DevStation.Application.Shared.Persistence;
using DevStation.Application.Shared.Providers;
using DevStation.Application.Shared.Results;
using DevStation.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DevStation.Application.Modules.Services.GetServiceHealth;

/// <summary>Ad-hoc check against service.Url: http(s) scheme → http probe,
/// otherwise host:port → tcp probe. Persists the status back onto the service.</summary>
public sealed class GetServiceHealthHandler(IDevStationDb db,
    IHealthCheckExecutor executor, IAuditWriter audit)
{
    public async Task<HealthResultDto> Handle(Guid id, CancellationToken ct)
    {
        var s = await db.Services.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new AppException("NOT_FOUND", 404, "Service not found");
        if (string.IsNullOrWhiteSpace(s.Url))
            throw new AppException("SERVICE_NOT_CHECKABLE", 422,
                "Service has no url/address to check");

        var kind = Uri.TryCreate(s.Url, UriKind.Absolute, out var uri) &&
                   uri.Scheme is "http" or "https" ? "http" : "tcp";
        var r = await executor.Execute(
            new HealthCheckDef(s.Id, kind, s.Url, 5), ct);

        s.Status = r.Status;
        s.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        await audit.Record("service.health", "service", s.Id, null,
            r.Status == HealthStatus.Healthy ? "ok" : "error",
            new { kind, s.Url, r.LatencyMs }, ct);
        return new HealthResultDto(r.Status.ToString().ToLowerInvariant(),
            r.LatencyMs, r.Error, r.CheckedAt);
    }
}
