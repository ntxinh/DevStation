using DevStation.Application.Modules.Health.CreateHealthCheck;
using DevStation.Application.Modules.Health.DeleteHealthCheck;
using DevStation.Application.Modules.Health.GetHealthOverview;
using DevStation.Application.Modules.Health.RunHealthCheck;
using Microsoft.Extensions.DependencyInjection;

namespace DevStation.Application.Modules.Health;

public static class HealthModule
{
    public static IServiceCollection AddHealthModule(this IServiceCollection s) => s
        .AddScoped<GetHealthOverviewHandler>()
        .AddScoped<RunHealthCheckHandler>()
        .AddScoped<CreateHealthCheckHandler>()
        .AddScoped<DeleteHealthCheckHandler>();
}
