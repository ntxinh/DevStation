using DevStation.Application.Shared.Providers;

namespace DevStation.Application.Modules.Containers.GetContainer;

public sealed class GetContainerHandler(IContainerProvider containers)
{
    public Task<ContainerInspect> Handle(string id, CancellationToken ct)
        => containers.InspectAsync(id, ct);
}
