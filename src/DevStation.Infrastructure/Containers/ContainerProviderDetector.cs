using DevStation.Application.Shared.Providers;
using DevStation.Application.Shared.Results;

namespace DevStation.Infrastructure.Containers;

public static class ContainerProviderDetector
{
    /// <summary>podman first, then docker, else null.</summary>
    public static string? Find(params string[] candidates)
    {
        var dirs = (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        foreach (var exe in candidates)
            if (dirs.Any(d => File.Exists(Path.Combine(d, exe))))
                return exe;
        return null;
    }

    public static IContainerProvider Detect(ContainerCliRunner runner) =>
        Find("podman", "docker") switch
        {
            "podman" => new PodmanCliProvider(runner),
            "docker" => new DockerCliProvider(runner),
            _ => NullContainerProvider.Instance,
        };
}

/// <summary>Runtime absent: lists read empty; anything that needs the daemon → 503.</summary>
public sealed class NullContainerProvider : IContainerProvider
{
    public static readonly NullContainerProvider Instance = new();

    public Task<IReadOnlyList<ContainerInfo>> ListAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<ContainerInfo>>([]);

    private static AppException Unavailable() => new("CONTAINER_RUNTIME_UNAVAILABLE",
        503, "No container runtime found (tried podman, docker)");

    public Task<ContainerInspect> InspectAsync(string id, CancellationToken ct = default)
        => throw Unavailable();
    public Task<IReadOnlyList<string>> GetLogsAsync(string id, int tail,
        CancellationToken ct = default) => throw Unavailable();
    public Task<ContainerStats?> GetStatsAsync(string id, CancellationToken ct = default)
        => throw Unavailable();
    public Task StartAsync(string id, CancellationToken ct = default) => throw Unavailable();
    public Task StopAsync(string id, CancellationToken ct = default) => throw Unavailable();
    public Task RestartAsync(string id, CancellationToken ct = default) => throw Unavailable();
}
