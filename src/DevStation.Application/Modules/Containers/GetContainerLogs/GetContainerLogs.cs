using DevStation.Application.Shared.Providers;

namespace DevStation.Application.Modules.Containers.GetContainerLogs;

public sealed class GetContainerLogsHandler(IContainerProvider containers)
{
    public Task<IReadOnlyList<string>> Handle(string id, int tail, CancellationToken ct)
        => containers.GetLogsAsync(id, tail, ct);
}
