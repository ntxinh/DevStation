using DevStation.Application.Shared.Auditing;
using DevStation.Application.Shared.Persistence;
using DevStation.Application.Shared.Providers;
using DevStation.Infrastructure.Auditing;
using DevStation.Infrastructure.Git;
using DevStation.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DevStation.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        // UseSnakeCaseNamingConvention is applied in DevStationDbContext.OnConfiguring
        services.AddDbContext<DevStationDbContext>(o =>
            o.UseNpgsql(config.GetConnectionString("Default")));
        services.AddHostedService<MigrationHostedService>();
        services.AddScoped<IDevStationDb>(sp => sp.GetRequiredService<DevStationDbContext>());
        services.AddScoped<IAuditWriter, AuditWriter>();
        services.AddSingleton<IGitProvider, LibGit2SharpGitProvider>();
        return services;
    }
}
