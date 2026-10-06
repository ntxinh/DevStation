using DevStation.Application.Shared.Persistence;
using DevStation.Domain.Entities;
using DevStation.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DevStation.Application.Modules.Dashboard.GetDashboardSummary;

public sealed record DashboardSummary(int Projects, int Services, int Containers,
    string EnvironmentHealth, List<AuditEvent> RecentActivity);

public sealed class GetDashboardSummaryHandler(IDevStationDb db)
{
    public async Task<DashboardSummary> Handle(CancellationToken ct)
    {
        var projects = await db.Projects.CountAsync(ct);
        var services = await db.Services.CountAsync(ct);

        // latest result per health check that belongs to an environment
        var latest = await db.HealthCheckResults
            .Where(r => db.HealthChecks.Any(h => h.Id == r.HealthCheckId && h.EnvironmentId != null))
            .GroupBy(r => r.HealthCheckId)
            .Select(g => g.OrderByDescending(r => r.CheckedAt).First().Status)
            .ToListAsync(ct);

        var health = latest.Count == 0 ? "unknown"
            : latest.Contains(HealthStatus.Unhealthy) ? "unhealthy"
            : latest.Contains(HealthStatus.Degraded) ? "degraded"
            : "healthy";

        var recent = await db.AuditEvents
            .OrderByDescending(a => a.Timestamp).Take(10).ToListAsync(ct);

        // ponytail: containers count 0 until IContainerProvider lands in Task 7
        return new DashboardSummary(projects, services, 0, health, recent);
    }
}

public static class DashboardModule
{
    public static IServiceCollection AddDashboardModule(this IServiceCollection s)
        => s.AddScoped<GetDashboardSummaryHandler>();
}
