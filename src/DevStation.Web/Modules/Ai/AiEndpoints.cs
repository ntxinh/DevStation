using DevStation.Application.Modules.Ai.ExplainError;
using DevStation.Application.Modules.Ai.ExplainSql;
using DevStation.Application.Modules.Ai.GenerateSql;
using DevStation.Application.Shared.Results;
using DevStation.Web.Shared.Modules;

namespace DevStation.Web.Modules.Ai;

/// <summary>
/// AI assist endpoints — Read policy because they only produce suggestions.
/// Returned SQL is advisory: execution still goes through
/// /api/databases/connections/{id}/execute with its own SqlGuard tiers.
/// </summary>
public static class AiEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        var g = api.MapGroup("/ai");

        g.MapPost("/explain-error", async (ExplainErrorRequest r,
                ExplainErrorHandler h, CancellationToken ct)
            => ApiResult.Ok(await h.Handle(r, ct)));

        g.MapPost("/explain-sql", async (ExplainSqlRequest r,
                ExplainSqlHandler h, CancellationToken ct)
            => ApiResult.Ok(await h.Handle(r, ct)));

        g.MapPost("/generate-sql", async (GenerateSqlRequest r,
                GenerateSqlHandler h, CancellationToken ct)
            => ApiResult.Ok(await h.Handle(r, ct)));
    }
}
