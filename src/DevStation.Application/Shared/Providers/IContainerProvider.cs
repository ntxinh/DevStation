using System.Text.RegularExpressions;
using DevStation.Application.Shared.Results;

namespace DevStation.Application.Shared.Providers;

/// <summary>Container id/name as accepted by podman/docker. Shared by Application
/// (service→container validation) and Infrastructure (argv construction) so ids can
/// never reach the CLI unvalidated.</summary>
public static class ContainerId
{
    private static readonly Regex Valid =
        new(@"^[a-zA-Z0-9][a-zA-Z0-9_.\-]{0,127}$", RegexOptions.Compiled);

    public static string Ensure(string id)
    {
        if (!Valid.IsMatch(id))
            throw new AppException("CONTAINER_ID_INVALID", 400,
                "Invalid container id or name");
        return id;
    }
}

public sealed record ContainerInfo(string Id, string Name, string Image, string State,
    string Status, IReadOnlyList<string> Ports);

public sealed record ContainerMount(string Type, string Source, string Destination,
    bool ReadWrite);

public sealed record ContainerNetwork(string Name, string? IpAddress);

public sealed record ContainerInspect(string Id, string Name, string Image, string State,
    int? ExitCode, DateTimeOffset? StartedAt, DateTimeOffset? CreatedAt,
    IReadOnlyList<string> Env, IReadOnlyList<ContainerMount> Mounts,
    IReadOnlyList<ContainerNetwork> Networks);

public sealed record ContainerStats(string Id, string Name, string? CpuPercent,
    string? MemUsage, string? MemPercent, string? NetIo, string? BlockIo, string? Pids);

/// <summary>Detected container runtime (podman preferred, docker fallback).
/// A registered <see cref="NullContainerProvider"/> reports "unavailable" instead of
/// failing — dashboards read an empty list, actions get CONTAINER_RUNTIME_UNAVAILABLE.</summary>
public interface IContainerProvider
{
    Task<IReadOnlyList<ContainerInfo>> ListAsync(CancellationToken ct = default);
    /// <summary>Env vars with names matching password|secret|token|key|credential
    /// (case-insensitive) are never emitted.</summary>
    Task<ContainerInspect> InspectAsync(string id, CancellationToken ct = default);
    /// <summary>Logs with timestamps; tail clamped to 1..5000.</summary>
    Task<IReadOnlyList<string>> GetLogsAsync(string id, int tail,
        CancellationToken ct = default);
    Task<ContainerStats?> GetStatsAsync(string id, CancellationToken ct = default);
    Task StartAsync(string id, CancellationToken ct = default);
    Task StopAsync(string id, CancellationToken ct = default);
    Task RestartAsync(string id, CancellationToken ct = default);
}
