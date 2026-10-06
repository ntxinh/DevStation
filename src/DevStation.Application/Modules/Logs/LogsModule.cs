using DevStation.Application.Modules.Audit.ListAuditEvents;
using DevStation.Application.Modules.Logs.GetLogs;
using DevStation.Application.Modules.Search.SearchAll;
using Microsoft.Extensions.DependencyInjection;

namespace DevStation.Application.Modules.Logs;

public static class LogsModule
{
    public static IServiceCollection AddLogsModule(this IServiceCollection s) => s
        .AddScoped<GetLogsHandler>()
        .AddScoped<ListAuditEventsHandler>()
        .AddScoped<SearchAllHandler>();
}
