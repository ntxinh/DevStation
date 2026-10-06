using System.Security.Claims;
using DevStation.Application.Shared.Auditing;
using DevStation.Application.Shared.Persistence;
using DevStation.Application.Shared.Providers;
using DevStation.Application.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace DevStation.Application.Modules.Deployments.StartDeployment;

public sealed record StartDeploymentRequest(Guid? ProjectId, string? Commit);

public sealed class StartDeploymentHandler(IDevStationDb db,
    IDeploymentProvider provider, IAuditWriter audit)
{
    /// <summary>Production envs need the Deploy policy; everything else Execute.
    /// Endpoint resolves this then runs AuthorizeAsync — the policy can't be
    /// a static attribute because it depends on the target env.</summary>
    public async Task<string> RequiredPolicyAsync(Guid environmentId, CancellationToken ct)
    {
        var type = await db.Environments.Where(e => e.Id == environmentId)
            .Select(e => e.Type).FirstOrDefaultAsync(ct)
            ?? throw new AppException("NOT_FOUND", 404, "Environment not found");
        return type == "production" ? "Deploy" : "Execute";
    }

    public async Task<DeploymentDto> Handle(Guid environmentId,
        StartDeploymentRequest r, ClaimsPrincipal user, CancellationToken ct)
    {
        if (r.ProjectId is { } pid &&
            !await db.Projects.AnyAsync(p => p.Id == pid, ct))
            throw new AppException("NOT_FOUND", 404, "Project not found");

        var id = await provider.StartAsync(r.ProjectId, environmentId, r.Commit, ct);
        // provider owns the row's lifecycle; stamp who pulled the trigger
        await db.Deployments.Where(d => d.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.TriggeredBy,
                user.Identity?.Name), ct);
        await audit.Record("deployment.start", "deployment", id,
            user.Identity?.Name, "ok",
            new { r.ProjectId, environmentId, r.Commit }, ct);

        var dto = await DeploymentsModule.Detail(db, id, null, ct);
        return dto ?? throw new AppException("INTERNAL_ERROR", 500,
            "Deployment row missing after start");
    }
}
