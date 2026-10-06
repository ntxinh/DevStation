using DevStation.Application.Modules.Environments.CompareEnvironments;
using DevStation.Application.Modules.Environments.CreateEnvironment;
using DevStation.Application.Modules.Environments.DeleteEnvironment;
using DevStation.Application.Modules.Environments.GetEnvironment;
using DevStation.Application.Modules.Environments.ListEnvironments;
using DevStation.Application.Modules.Environments.UpdateEnvironment;
using DevStation.Application.Shared.Persistence;
using DevStation.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DevStation.Application.Modules.Environments;

public static class EnvironmentsModule
{
    public static IServiceCollection AddEnvironmentsModule(this IServiceCollection s) => s
        .AddScoped<ListEnvironmentsHandler>()
        .AddScoped<GetEnvironmentHandler>()
        .AddScoped<CreateEnvironmentHandler>()
        .AddScoped<UpdateEnvironmentHandler>()
        .AddScoped<DeleteEnvironmentHandler>()
        .AddScoped<CompareEnvironmentsHandler>();

    /// <summary>Aggregate of each environment's latest check result → lowercase HealthStatus.</summary>
    public static async Task<Dictionary<Guid, string>> LatestHealthByEnvironment(
        IDevStationDb db, CancellationToken ct)
    {
        var checks = await db.HealthChecks
            .Where(h => h.EnvironmentId != null && h.Enabled)
            .Select(h => new { h.Id, EnvId = h.EnvironmentId!.Value })
            .ToListAsync(ct);
        var ids = checks.Select(c => c.Id).ToList();
        var results = await db.HealthCheckResults
            .Where(r => ids.Contains(r.HealthCheckId))
            .ToListAsync(ct);
        // latest result per check, then per env: unhealthy > degraded > healthy
        return checks
            .Select(c => (
                c.EnvId,
                results.Where(r => r.HealthCheckId == c.Id)
                       .MaxBy(r => r.CheckedAt)?.Status))
            .GroupBy(x => x.EnvId)
            .ToDictionary(g => g.Key, g => Aggregate(g.Select(x => x.Status)));
    }

    public static string Aggregate(IEnumerable<HealthStatus?> statuses)
    {
        var s = statuses.Where(x => x.HasValue).Select(x => x!.Value).ToList();
        if (s.Count == 0) return "unknown";
        if (s.Contains(HealthStatus.Unhealthy)) return "unhealthy";
        if (s.Contains(HealthStatus.Degraded)) return "degraded";
        if (s.Contains(HealthStatus.Unknown)) return "unknown";
        return "healthy";
    }
}
