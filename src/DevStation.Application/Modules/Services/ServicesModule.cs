using DevStation.Application.Modules.Services.CreateService;
using DevStation.Application.Modules.Services.GetServiceHealth;
using DevStation.Application.Modules.Services.ListServices;
using DevStation.Application.Modules.Services.RestartService;
using Microsoft.Extensions.DependencyInjection;

namespace DevStation.Application.Modules.Services;

public static class ServicesModule
{
    public static IServiceCollection AddServicesModule(this IServiceCollection s) => s
        .AddScoped<ListServicesHandler>()
        .AddScoped<CreateServiceHandler>()
        .AddScoped<GetServiceHealthHandler>()
        .AddScoped<RestartServiceHandler>();
}
