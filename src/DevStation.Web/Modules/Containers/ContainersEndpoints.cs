using System.Security.Claims;
using DevStation.Application.Modules.Containers.GetContainer;
using DevStation.Application.Modules.Containers.GetContainerLogs;
using DevStation.Application.Modules.Containers.GetContainerStats;
using DevStation.Application.Modules.Containers.ListContainers;
using DevStation.Application.Modules.Containers.RestartContainer;
using DevStation.Application.Modules.Containers.StartContainer;
using DevStation.Application.Modules.Containers.StopContainer;
using DevStation.Application.Shared.Results;
using DevStation.Web.Shared.Modules;

namespace DevStation.Web.Modules.Containers;

public static class ContainersEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/containers", async (ListContainersHandler h,
                CancellationToken ct) => ApiResult.Ok(await h.Handle(ct)));
        api.MapGet("/containers/{id}", async (string id,
                GetContainerHandler h, CancellationToken ct)
            => ApiResult.Ok(await h.Handle(id, ct)));
        api.MapGet("/containers/{id}/logs", async (string id, int? tail,
                GetContainerLogsHandler h, CancellationToken ct)
            => ApiResult.Ok(await h.Handle(id, tail ?? 200, ct)));
        api.MapGet("/containers/{id}/stats", async (string id,
                GetContainerStatsHandler h, CancellationToken ct)
            => ApiResult.Ok(await h.Handle(id, ct)));

        api.MapPost("/containers/{id}/start", async (string id, bool? confirm,
                StartContainerHandler h, ClaimsPrincipal u, CancellationToken ct)
            => { ConfirmGate.Require(confirm == true);
                 await h.Handle(id, u, ct); return ApiResult.Ok(new { started = true }); })
            .RequireAuthorization("Execute");
        api.MapPost("/containers/{id}/stop", async (string id, bool? confirm,
                StopContainerHandler h, ClaimsPrincipal u, CancellationToken ct)
            => { ConfirmGate.Require(confirm == true);
                 await h.Handle(id, u, ct); return ApiResult.Ok(new { stopped = true }); })
            .RequireAuthorization("Execute");
        api.MapPost("/containers/{id}/restart", async (string id, bool? confirm,
                RestartContainerHandler h, ClaimsPrincipal u, CancellationToken ct)
            => { ConfirmGate.Require(confirm == true);
                 await h.Handle(id, u, ct); return ApiResult.Ok(new { restarted = true }); })
            .RequireAuthorization("Execute");
    }
}
