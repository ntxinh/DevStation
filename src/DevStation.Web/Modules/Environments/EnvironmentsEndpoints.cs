using System.Security.Claims;
using DevStation.Application.Modules.Environments.CompareEnvironments;
using DevStation.Application.Modules.Environments.CreateEnvironment;
using DevStation.Application.Modules.Environments.DeleteEnvironment;
using DevStation.Application.Modules.Environments.GetEnvironment;
using DevStation.Application.Modules.Environments.ListEnvironments;
using DevStation.Application.Modules.Environments.UpdateEnvironment;
using DevStation.Application.Shared.Results;
using DevStation.Web.Shared.Modules;

namespace DevStation.Web.Modules.Environments;

public static class EnvironmentsEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/environments", async (ListEnvironmentsHandler h, CancellationToken ct)
            => ApiResult.Ok(await h.Handle(ct)));
        // literal segment wins over {id:guid} — compare must map before detail
        api.MapGet("/environments/compare", async (Guid a, Guid b,
                CompareEnvironmentsHandler h, CancellationToken ct)
            => ApiResult.Ok(await h.Handle(a, b, ct)));
        api.MapGet("/environments/{id:guid}", async (Guid id,
                GetEnvironmentHandler h, CancellationToken ct)
            => ApiResult.Ok(await h.Handle(id, ct)));
        api.MapPost("/environments", async (CreateEnvironmentRequest r,
                CreateEnvironmentHandler h, ClaimsPrincipal u, CancellationToken ct)
            => ApiResult.Ok(await h.Handle(r, u, ct)))
            .RequireAuthorization("Execute");
        api.MapPatch("/environments/{id:guid}", async (Guid id,
                UpdateEnvironmentRequest r, UpdateEnvironmentHandler h,
                ClaimsPrincipal u, CancellationToken ct)
            => ApiResult.Ok(await h.Handle(id, r, u, ct)))
            .RequireAuthorization("Execute");
        api.MapDelete("/environments/{id:guid}", async (Guid id, bool? confirm,
                DeleteEnvironmentHandler h, ClaimsPrincipal u, CancellationToken ct)
            =>
        {
            ConfirmGate.Require(confirm == true);
            await h.Handle(id, u, ct); return ApiResult.Ok(new { deleted = true });
        })
            .RequireAuthorization("Execute");
    }
}
