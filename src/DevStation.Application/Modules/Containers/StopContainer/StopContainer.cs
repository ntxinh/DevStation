using DevStation.Application.Shared.Auditing;
using DevStation.Application.Shared.Providers;

namespace DevStation.Application.Modules.Containers.StopContainer;

public sealed class StopContainerHandler(IContainerProvider c, IAuditWriter a)
    : ContainerActionHandler(c, a, "stop")
{
    protected override Task Act(IContainerProvider p, string id, CancellationToken ct)
        => p.StopAsync(id, ct);
}
