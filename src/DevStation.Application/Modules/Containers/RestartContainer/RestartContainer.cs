using DevStation.Application.Shared.Auditing;
using DevStation.Application.Shared.Providers;

namespace DevStation.Application.Modules.Containers.RestartContainer;

public sealed class RestartContainerHandler(IContainerProvider c, IAuditWriter a)
    : ContainerActionHandler(c, a, "restart")
{
    protected override Task Act(IContainerProvider p, string id, CancellationToken ct)
        => p.RestartAsync(id, ct);
}
