using System.Security.Claims;
using DevStation.Application.Modules.Deployments.CancelDeployment;
using DevStation.Application.Modules.Deployments.GetDeployment;
using DevStation.Application.Modules.Deployments.ListDeployments;
using DevStation.Application.Modules.Deployments.StartDeployment;
using DevStation.Application.Shared.Results;
using DevStation.Web.Shared.Modules;
using Microsoft.AspNetCore.Authorization;

namespace DevStation.Web.Modules.Deployments;

public static class DeploymentsEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/deployments", async (Guid? environmentId, Guid? projectId,
                int? take, ListDeploymentsHandler h, CancellationToken ct)
            => ApiResult.Ok(await h.Handle(environmentId, projectId, take, ct)));

        api.MapGet("/deployments/{id:guid}", async (Guid id,
                GetDeploymentHandler h, CancellationToken ct)
            => ApiResult.Ok(await h.Handle(id, ct)));

        // POST: confirm always; Execute for non-prod envs, Deploy for
        // env.type=="production" — resolved per-request, then the standard
        // policy pipeline runs it.
        api.MapPost("/deployments", async (Guid environmentId, bool? confirm,
                StartDeploymentRequest r, StartDeploymentHandler h,
                IAuthorizationService auth, ClaimsPrincipal u,
                CancellationToken ct) =>
            {
                ConfirmGate.Require(confirm == true);
                var policy = await h.RequiredPolicyAsync(environmentId, ct);
                if (!(await auth.AuthorizeAsync(u, null, policy)).Succeeded)
                    return Results.Forbid();
                return ApiResult.Ok(await h.Handle(environmentId, r, u, ct));
            });

        api.MapPost("/deployments/{id:guid}/cancel", async (Guid id,
                CancelDeploymentHandler h, ClaimsPrincipal u,
                CancellationToken ct)
            => ApiResult.Ok(await h.Handle(id, u, ct)))
            .RequireAuthorization("Execute");
    }
}
