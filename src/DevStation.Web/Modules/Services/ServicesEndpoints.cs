using System.Security.Claims;
using DevStation.Application.Modules.Services.CreateService;
using DevStation.Application.Modules.Services.GetServiceHealth;
using DevStation.Application.Modules.Services.ListServices;
using DevStation.Application.Modules.Services.RestartService;
using DevStation.Application.Shared.Results;
using DevStation.Web.Shared.Modules;

namespace DevStation.Web.Modules.Services;

public static class ServicesEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/services", async (Guid? environmentId,
                ListServicesHandler h, CancellationToken ct)
            => ApiResult.Ok(await h.Handle(environmentId, ct)));
        api.MapPost("/services", async (CreateServiceRequest r,
                CreateServiceHandler h, ClaimsPrincipal u, CancellationToken ct)
            => ApiResult.Ok(await h.Handle(r, u, ct)))
            .RequireAuthorization("Execute");
        api.MapGet("/services/{id:guid}/health", async (Guid id,
                GetServiceHealthHandler h, ClaimsPrincipal u, CancellationToken ct)
            => ApiResult.Ok(await h.Handle(id, u, ct)))
            .RequireAuthorization("Execute");
        api.MapPost("/services/{id:guid}/restart", async (Guid id, bool? confirm,
                RestartServiceHandler h, ClaimsPrincipal u, CancellationToken ct)
            =>
        {
            ConfirmGate.Require(confirm == true);
            return ApiResult.Ok(await h.Handle(id, u, ct));
        })
            .RequireAuthorization("Execute");
    }
}
