using DevStation.Application.Modules.Containers.GetContainer;
using DevStation.Application.Modules.Containers.GetContainerLogs;
using DevStation.Application.Modules.Containers.GetContainerStats;
using DevStation.Application.Modules.Containers.ListContainers;
using DevStation.Application.Modules.Containers.RestartContainer;
using DevStation.Application.Modules.Containers.StartContainer;
using DevStation.Application.Modules.Containers.StopContainer;
using Microsoft.Extensions.DependencyInjection;

namespace DevStation.Application.Modules.Containers;

public static class ContainersModule
{
    public static IServiceCollection AddContainersModule(this IServiceCollection s) => s
        .AddScoped<ListContainersHandler>()
        .AddScoped<GetContainerHandler>()
        .AddScoped<StartContainerHandler>()
        .AddScoped<StopContainerHandler>()
        .AddScoped<RestartContainerHandler>()
        .AddScoped<GetContainerLogsHandler>()
        .AddScoped<GetContainerStatsHandler>();
}
