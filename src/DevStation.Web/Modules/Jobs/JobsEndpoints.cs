using System.Security.Claims;
using DevStation.Application.Modules.Jobs.GetJob;
using DevStation.Application.Modules.Jobs.ListJobs;
using DevStation.Application.Modules.Jobs.PauseJob;
using DevStation.Application.Modules.Jobs.ResumeJob;
using DevStation.Application.Modules.Jobs.TriggerJob;
using DevStation.Application.Shared.Results;
using DevStation.Web.Shared.Modules;

namespace DevStation.Web.Modules.Jobs;

public static class JobsEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/jobs", async (ListJobsHandler h, CancellationToken ct)
            => ApiResult.Ok(await h.Handle(ct)));
        api.MapGet("/jobs/{id:guid}", async (Guid id, GetJobHandler h,
                CancellationToken ct) => ApiResult.Ok(await h.Handle(id, ct)));
        api.MapPost("/jobs/{id:guid}/trigger", async (Guid id, bool? confirm,
                TriggerJobHandler h, ClaimsPrincipal u, CancellationToken ct)
            =>
        {
            ConfirmGate.Require(confirm == true);
            return ApiResult.Ok(await h.Handle(id, u, ct));
        })
            .RequireAuthorization("Execute");
        api.MapPost("/jobs/{id:guid}/pause", async (Guid id, PauseJobHandler h,
                ClaimsPrincipal u, CancellationToken ct)
            => ApiResult.Ok(await h.Handle(id, u, ct)))
            .RequireAuthorization("Execute");
        api.MapPost("/jobs/{id:guid}/resume", async (Guid id, ResumeJobHandler h,
                ClaimsPrincipal u, CancellationToken ct)
            => ApiResult.Ok(await h.Handle(id, u, ct)))
            .RequireAuthorization("Execute");
    }
}
