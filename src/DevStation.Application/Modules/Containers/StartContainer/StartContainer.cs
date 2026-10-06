using DevStation.Application.Shared.Auditing;
using DevStation.Application.Shared.Providers;

namespace DevStation.Application.Modules.Containers.StartContainer;

public sealed class StartContainerHandler(IContainerProvider c, IAuditWriter a)
    : ContainerActionHandler(c, a, "start")
{
    protected override Task Act(IContainerProvider p, string id, CancellationToken ct)
        => p.StartAsync(id, ct);
}
