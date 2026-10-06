using System.Security.Claims;
using DevStation.Application.Shared.Auditing;
using DevStation.Application.Shared.Providers;

namespace DevStation.Application.Modules.Containers;

/// <summary>start/stop/restart — all audited as container.{action}.
/// AuditEvent.ResourceId is a Guid; the container id rides in metadata.</summary>
public abstract class ContainerActionHandler(IContainerProvider containers,
    IAuditWriter audit, string action)
{
    protected abstract Task Act(IContainerProvider c, string id, CancellationToken ct);

    public async Task Handle(string id, ClaimsPrincipal user, CancellationToken ct)
    {
        await Act(containers, id, ct);
        await audit.Record($"container.{action}", "container", null,
            user.Identity?.Name, "ok", new { id }, ct);
    }
}
