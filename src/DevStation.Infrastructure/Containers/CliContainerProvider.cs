using DevStation.Application.Shared.Providers;

namespace DevStation.Infrastructure.Containers;

/// <summary>Shared podman/docker provider — the CLIs are argument-compatible.
/// Every id is validated by <see cref="ContainerId.Ensure"/> before it reaches argv.</summary>
public abstract class CliContainerProvider(ContainerCliRunner runner, string exe)
    : IContainerProvider
{
    public string Runtime => exe;

    public async Task<IReadOnlyList<ContainerInfo>> ListAsync(CancellationToken ct = default)
        => ContainerOutputParser.ParseList(
            await runner.RunAsync(exe, ["ps", "-a", "--format", "json"], ct));

    public async Task<ContainerInspect> InspectAsync(string id, CancellationToken ct = default)
        => ContainerOutputParser.ParseInspect(
            await runner.RunAsync(exe, ["inspect", ContainerId.Ensure(id)], ct));

    public async Task<IReadOnlyList<string>> GetLogsAsync(string id, int tail,
        CancellationToken ct = default)
    {
        var n = Math.Clamp(tail, 1, 5000); // default 200 applied at the endpoint
        var s = await runner.RunAsync(exe,
            ["logs", "--timestamps", "--tail", n.ToString(), ContainerId.Ensure(id)], ct);
        return s.Split('\n', StringSplitOptions.RemoveEmptyEntries);
    }

    public async Task<ContainerStats?> GetStatsAsync(string id, CancellationToken ct = default)
        => ContainerOutputParser.ParseStats(await runner.RunAsync(exe,
               ["stats", "--no-stream", "--format", "json", ContainerId.Ensure(id)], ct))
           .FirstOrDefault();

    public Task StartAsync(string id, CancellationToken ct = default)
        => runner.RunAsync(exe, ["start", ContainerId.Ensure(id)], ct);

    public Task StopAsync(string id, CancellationToken ct = default)
        => runner.RunAsync(exe, ["stop", ContainerId.Ensure(id)], ct);

    public Task RestartAsync(string id, CancellationToken ct = default)
        => runner.RunAsync(exe, ["restart", ContainerId.Ensure(id)], ct);
}

public sealed class PodmanCliProvider(ContainerCliRunner runner)
    : CliContainerProvider(runner, "podman");

public sealed class DockerCliProvider(ContainerCliRunner runner)
    : CliContainerProvider(runner, "docker");
