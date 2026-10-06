using DevStation.Application.Shared.Persistence;
using DevStation.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DevStation.Application.Modules.Health.GetHealthOverview;

public sealed record HealthOverviewItem(HealthCheckDto Check, HealthResultDto? Last,
    double Uptime24h);

public sealed record HealthOverview(List<HealthOverviewItem> Checks);

public sealed class GetHealthOverviewHandler(IDevStationDb db)
{
    public async Task<HealthOverview> Handle(CancellationToken ct)
    {
        var checks = await db.HealthChecks.OrderBy(h => h.Name).ToListAsync(ct);
        var since = DateTimeOffset.UtcNow.AddHours(-24);
        var ids = checks.Select(c => c.Id).ToList();
        var results = await db.HealthCheckResults
            .Where(r => ids.Contains(r.HealthCheckId) && r.CheckedAt >= since)
            .ToListAsync(ct);

        var items = checks.Select(c =>
        {
            var rs = results.Where(r => r.HealthCheckId == c.Id).ToList();
            var last = rs.MaxBy(r => r.CheckedAt);
            // % of results that were Healthy over the window; null → 0
            var uptime = rs.Count == 0 ? 0d
                : Math.Round(100d * rs.Count(r => r.Status == HealthStatus.Healthy) / rs.Count, 1);
            return new HealthOverviewItem(HealthCheckDto.Of(c, last),
                last is null ? null : HealthResultDto.Of(last), uptime);
        }).ToList();
        return new HealthOverview(items);
    }
}
