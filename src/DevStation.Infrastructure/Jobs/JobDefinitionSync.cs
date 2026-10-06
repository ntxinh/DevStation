using DevStation.Application.Shared.Persistence;
using DevStation.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DevStation.Infrastructure.Jobs;

/// <summary>
/// Materializes one job_definitions row per configured `Jobs:` registration at
/// startup (upsert by name). Registered after MigrationHostedService so tables
/// exist. Stale defs from removed config entries are kept — they hold history.
/// </summary>
public sealed class JobDefinitionSync(
    IServiceScopeFactory scopeFactory,
    IReadOnlyList<JobRegistration> registrations,
    ILogger<JobDefinitionSync> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        if (registrations.Count == 0) return;
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IDevStationDb>();
        var existing = await db.JobDefinitions
            .Where(d => registrations.Select(r => r.Name).Contains(d.Name))
            .ToListAsync(ct);
        foreach (var reg in registrations)
        {
            var def = existing.FirstOrDefault(d =>
                string.Equals(d.Name, reg.Name, StringComparison.OrdinalIgnoreCase));
            if (def is null)
            {
                def = new JobDefinition { Name = reg.Name };
                db.JobDefinitions.Add(def);
            }
            def.Type = reg.Type;
            def.Description ??= reg.Description;
            def.Cron = reg.Cron;
            def.Timezone = reg.Timezone;
        }
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Synced {Count} job definitions", registrations.Count);
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
