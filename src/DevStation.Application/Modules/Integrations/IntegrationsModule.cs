using DevStation.Application.Modules.Integrations.ConnectIntegration;
using DevStation.Application.Modules.Integrations.DisconnectIntegration;
using DevStation.Application.Modules.Integrations.ListIntegrations;
using DevStation.Application.Modules.Integrations.TestIntegration;
using Microsoft.Extensions.DependencyInjection;

namespace DevStation.Application.Modules.Integrations;

public static class IntegrationsModule
{
    public static IServiceCollection AddIntegrationsModule(this IServiceCollection s) => s
        .AddScoped<ListIntegrationsHandler>()
        .AddScoped<ConnectIntegrationHandler>()
        .AddScoped<TestIntegrationHandler>()
        .AddScoped<DisconnectIntegrationHandler>();
}
