using System.Security.Claims;
using DevStation.Application.Modules.Health.CreateHealthCheck;
using DevStation.Application.Modules.Health.DeleteHealthCheck;
using DevStation.Application.Modules.Health.GetHealthOverview;
using DevStation.Application.Modules.Health.RunHealthCheck;
using DevStation.Application.Shared.Results;
using DevStation.Web.Shared.Modules;

namespace DevStation.Web.Modules.Health;

public static class HealthEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/health/overview", async (GetHealthOverviewHandler h,
                CancellationToken ct) => ApiResult.Ok(await h.Handle(ct)));
        api.MapPost("/health", async (CreateHealthCheckRequest r,
                CreateHealthCheckHandler h, ClaimsPrincipal u, CancellationToken ct)
            => ApiResult.Ok(await h.Handle(r, u, ct)))
            .RequireAuthorization("Execute");
        api.MapPost("/health/{id:guid}/check", async (Guid id,
                RunHealthCheckHandler h, CancellationToken ct)
            => ApiResult.Ok(await h.Handle(id, ct)))
            .RequireAuthorization("Execute");
        api.MapDelete("/health/{id:guid}", async (Guid id, bool? confirm,
                DeleteHealthCheckHandler h, ClaimsPrincipal u, CancellationToken ct)
            => { ConfirmGate.Require(confirm == true);
                 await h.Handle(id, u, ct); return ApiResult.Ok(new { deleted = true }); })
            .RequireAuthorization("Execute");
    }
}
