using DevStation.Application.Modules.Commands.ExecuteCommand;
using DevStation.Application.Modules.Commands.ListCommands;
using DevStation.Application.Shared.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace DevStation.Application.Modules.Commands;

public static class CommandsModule
{
    public static IServiceCollection AddCommandsModule(this IServiceCollection s) => s
        .AddScoped<CommandRegistry>()
        .AddScoped<ListCommandsHandler>()
        .AddScoped<ExecuteCommandHandler>();
}
