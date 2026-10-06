using Microsoft.Extensions.DependencyInjection;

namespace DevStation.Application.Modules.Mcp;

public static class McpModule
{
    public static IServiceCollection AddMcpModule(this IServiceCollection s) => s
        .AddScoped<McpToolInvoker>();
}
