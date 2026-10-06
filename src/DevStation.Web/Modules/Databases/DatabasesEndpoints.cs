using System.Security.Claims;
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
using DevStation.Application.Shared.Results;
using DevStation.Web.Shared.Modules;

namespace DevStation.Web.Modules.Databases;

public static class DatabasesEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/databases/connections", async (ListConnectionsHandler h,
                CancellationToken ct) => ApiResult.Ok(await h.Handle(ct)));
        api.MapPost("/databases/connections", async (CreateConnectionRequest r,
                CreateConnectionHandler h, ClaimsPrincipal u, CancellationToken ct)
            => ApiResult.Ok(await h.Handle(r, u, ct)))
            .RequireAuthorization("Execute");
        api.MapPatch("/databases/connections/{id:guid}", async (Guid id,
                UpdateConnectionRequest r, UpdateConnectionHandler h,
                ClaimsPrincipal u, CancellationToken ct)
            => ApiResult.Ok(await h.Handle(id, r, u, ct)))
            .RequireAuthorization("Execute");
        api.MapDelete("/databases/connections/{id:guid}", async (Guid id,
                bool? confirm, DeleteConnectionHandler h, ClaimsPrincipal u,
                CancellationToken ct)
            =>
        {
            ConfirmGate.Require(confirm == true);
            await h.Handle(id, u, ct); return ApiResult.Ok(new { deleted = true });
        })
            .RequireAuthorization("Execute");
        api.MapPost("/databases/connections/{id:guid}/test", async (Guid id,
                TestConnectionHandler h, ClaimsPrincipal u, CancellationToken ct)
            => ApiResult.Ok(await h.Handle(id, u, ct)))
            .RequireAuthorization("Execute");
        api.MapGet("/databases/connections/{id:guid}/schema", async (Guid id,
                GetSchemaHandler h, CancellationToken ct)
            => ApiResult.Ok(await h.Handle(id, ct)));

        // guardrails live in the handler; ?confirm=true unlocks Dangerous tier
        api.MapPost("/databases/connections/{id:guid}/execute", async (Guid id,
                ExecuteQueryRequest r, bool? confirm, ExecuteQueryHandler h,
                ClaimsPrincipal u, CancellationToken ct)
            => ApiResult.Ok(await h.Handle(id, r, confirm == true, u, ct)))
            .RequireAuthorization("Execute");

        api.MapGet("/databases/history", async (Guid? connectionId, int? limit,
                ListQueryHistoryHandler h, CancellationToken ct)
            => ApiResult.Ok(await h.Handle(connectionId, limit ?? 100, ct)));

        api.MapGet("/databases/saved-queries", async (Guid? connectionId,
                ListSavedQueriesHandler h, CancellationToken ct)
            => ApiResult.Ok(await h.Handle(connectionId, ct)));
        api.MapPost("/databases/saved-queries", async (SaveQueryRequest r,
                SaveQueryHandler h, ClaimsPrincipal u, CancellationToken ct)
            => ApiResult.Ok(await h.Handle(r, u, ct)))
            .RequireAuthorization("Execute");
        api.MapDelete("/databases/saved-queries/{id:guid}", async (Guid id,
                DeleteSavedQueryHandler h, ClaimsPrincipal u, CancellationToken ct)
            =>
        { await h.Handle(id, u, ct); return ApiResult.Ok(new { deleted = true }); })
            .RequireAuthorization("Execute");
    }
}
