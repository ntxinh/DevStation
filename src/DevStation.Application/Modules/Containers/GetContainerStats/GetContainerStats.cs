using DevStation.Application.Shared.Providers;

namespace DevStation.Application.Modules.Containers.GetContainerStats;

public sealed class GetContainerStatsHandler(IContainerProvider containers)
{
    public Task<ContainerStats?> Handle(string id, CancellationToken ct)
        => containers.GetStatsAsync(id, ct);
}
