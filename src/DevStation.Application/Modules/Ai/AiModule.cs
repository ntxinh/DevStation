using DevStation.Application.Modules.Ai.ExplainError;
using DevStation.Application.Modules.Ai.ExplainSql;
using DevStation.Application.Modules.Ai.GenerateSql;
using Microsoft.Extensions.DependencyInjection;

namespace DevStation.Application.Modules.Ai;

public static class AiModule
{
    public static IServiceCollection AddAiModule(this IServiceCollection s) => s
        .AddScoped<ExplainErrorHandler>()
        .AddScoped<ExplainSqlHandler>()
        .AddScoped<GenerateSqlHandler>();
}
