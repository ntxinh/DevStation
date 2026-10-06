using DevStation.Application.Modules.Databases.CreateConnection;
using DevStation.Application.Modules.Databases.DeleteConnection;
using DevStation.Application.Modules.Databases.DeleteSavedQuery;
using DevStation.Application.Modules.Databases.ExecuteQuery;
using DevStation.Application.Modules.Databases.GetSchema;
using DevStation.Application.Modules.Databases.ListConnections;
using DevStation.Application.Modules.Databases.ListQueryHistory;
using DevStation.Application.Modules.Databases.ListSavedQueries;
using DevStation.Application.Modules.Databases.SaveQuery;
using DevStation.Application.Modules.Databases.TestConnection;
using DevStation.Application.Modules.Databases.UpdateConnection;
using Microsoft.Extensions.DependencyInjection;

namespace DevStation.Application.Modules.Databases;

public static class DatabasesModule
{
    public static IServiceCollection AddDatabasesModule(this IServiceCollection s) => s
        .AddScoped<ListConnectionsHandler>()
        .AddScoped<CreateConnectionHandler>()
        .AddScoped<UpdateConnectionHandler>()
        .AddScoped<DeleteConnectionHandler>()
        .AddScoped<TestConnectionHandler>()
        .AddScoped<GetSchemaHandler>()
        .AddScoped<ExecuteQueryHandler>()
        .AddScoped<ListQueryHistoryHandler>()
        .AddScoped<ListSavedQueriesHandler>()
        .AddScoped<SaveQueryHandler>()
        .AddScoped<DeleteSavedQueryHandler>();
}
