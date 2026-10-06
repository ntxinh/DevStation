using System.Security.Claims;
using DevStation.Application.Modules.Projects.CreateProject;
using DevStation.Application.Modules.Projects.DeleteProject;
using DevStation.Application.Modules.Projects.GetGitStatus;
using DevStation.Application.Modules.Projects.GetProject;
using DevStation.Application.Modules.Projects.GetRecentCommits;
using DevStation.Application.Modules.Projects.ListProjects;
using DevStation.Application.Modules.Projects.UpdateProject;
using DevStation.Application.Shared.Results;

using DevStation.Web.Shared.Modules;
namespace DevStation.Web.Modules.Projects;

public static class ProjectsEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/projects", async (string? q, ListProjectsHandler h, CancellationToken ct)
            => ApiResult.Ok(await h.Handle(q, ct)));
        api.MapPost("/projects", async (CreateProjectRequest r, CreateProjectHandler h,
                ClaimsPrincipal u, CancellationToken ct)
            => ApiResult.Ok(await h.Handle(r, u, ct)))
            .RequireAuthorization("Execute");
        api.MapGet("/projects/{id:guid}", async (Guid id, GetProjectHandler h, CancellationToken ct)
            => ApiResult.Ok(await h.Handle(id, ct)));
        api.MapPatch("/projects/{id:guid}", async (Guid id, UpdateProjectRequest r,
                UpdateProjectHandler h, ClaimsPrincipal u, CancellationToken ct)
            => ApiResult.Ok(await h.Handle(id, r, u, ct)))
            .RequireAuthorization("Execute");
        api.MapDelete("/projects/{id:guid}", async (Guid id, bool? confirm,
                DeleteProjectHandler h, ClaimsPrincipal u, CancellationToken ct)
            => { ConfirmGate.Require(confirm == true);
                 await h.Handle(id, u, ct); return ApiResult.Ok(new { deleted = true }); })
            .RequireAuthorization("Execute");
        api.MapGet("/projects/{id:guid}/git", async (Guid id, GetGitStatusHandler h, CancellationToken ct)
            => ApiResult.Ok(await h.Handle(id, ct)));
        api.MapGet("/projects/{id:guid}/commits", async (Guid id, int? n,
                GetRecentCommitsHandler h, CancellationToken ct)
            => ApiResult.Ok(await h.Handle(id, n ?? 10, ct)));
    }
}
