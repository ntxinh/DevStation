using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DevStation.Infrastructure.Persistence;

/// <summary>Applies pending EF migrations at startup; failure is fatal (log + rethrow).</summary>
public sealed class MigrationHostedService(IServiceScopeFactory scopeFactory, ILogger<MigrationHostedService> log) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            await using var db = scope.ServiceProvider.GetRequiredService<DevStationDbContext>();
            log.LogInformation("Applying database migrations");
            await db.Database.MigrateAsync(ct);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Database migration failed");
            throw;
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
