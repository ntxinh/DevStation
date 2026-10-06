using DevStation.Application.Shared.Providers;

namespace DevStation.Application.Modules.Containers.ListContainers;

public sealed class ListContainersHandler(IContainerProvider containers)
{
    public Task<IReadOnlyList<ContainerInfo>> Handle(CancellationToken ct)
        => containers.ListAsync(ct);
}
