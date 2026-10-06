using System.Security.Claims;
using DevStation.Application.Shared.Auditing;
using DevStation.Application.Shared.Persistence;
using DevStation.Application.Shared.Providers;
using DevStation.Application.Shared.Results;

namespace DevStation.Application.Modules.Deployments.CancelDeployment;

public sealed class CancelDeploymentHandler(IDevStationDb db,
    IDeploymentProvider provider, IAuditWriter audit)
{
    /// <summary>queued/running → cancelled; finished deployments 409
    /// (raised by the provider, which owns the state machine).</summary>
    public async Task<DeploymentDto> Handle(Guid id, ClaimsPrincipal user, CancellationToken ct)
    {
        await provider.CancelAsync(id, ct);
        await audit.Record("deployment.cancel", "deployment", id,
            user.Identity?.Name, "ok", null, ct);
        return await DeploymentsModule.Detail(db, id, null, ct)
            ?? throw new AppException("NOT_FOUND", 404, "Deployment not found");
    }
}
