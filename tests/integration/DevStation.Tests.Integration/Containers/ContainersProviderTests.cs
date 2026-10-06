using DevStation.Application.Shared.Results;
using DevStation.Infrastructure.Containers;

namespace DevStation.Tests.Integration.Containers;

/// <summary>Runs against the real runtime when one is on PATH.
/// Early-returns (skips) when podman is absent or the busybox image can't be pulled.</summary>
public sealed class ContainersProviderTests
{
    private static CliContainerProvider? Provider()
        => ContainerProviderDetector.Find("podman", "docker") switch
        {
            "podman" => new PodmanCliProvider(new ContainerCliRunner()),
            "docker" => new DockerCliProvider(new ContainerCliRunner()),
            _ => null,
        };

    [Fact]
    public async Task ListAsync_executes_and_parses()
    {
        var p = Provider();
        if (p is null) return; // no runtime on PATH
        var list = await p.ListAsync();
        Assert.NotNull(list); // empty is fine — the point is exec + parse didn't throw
    }

    [Fact]
    public async Task Full_lifecycle_against_real_container()
    {
        var p = Provider();
        if (p is null) return;
        var runner = new ContainerCliRunner();
        var exe = p.Runtime;

        // pull distinguishes "offline" from real failures
        try { await runner.RunAsync(exe, ["pull", "-q", "docker.io/library/busybox:latest"], CancellationToken.None); }
        catch (AppException) { return; } // no registry access — skip

        var name = $"devstation-it-{Guid.NewGuid():N}"[..20];
        await runner.RunAsync(exe,
            ["run", "-d", "--name", name, "docker.io/library/busybox:latest", "sleep", "30"],
            CancellationToken.None);
        try
        {
            var list = await p.ListAsync();
            var c = Assert.Single(list, x => x.Name == name);
            Assert.Equal("running", c.State);

            var inspect = await p.InspectAsync(name);
            Assert.Equal(name, inspect.Name);

            await p.StopAsync(name);
            Assert.Equal("exited", (await p.InspectAsync(name)).State);

            await p.StartAsync(name);
            Assert.Equal("running", (await p.InspectAsync(name)).State);

            await p.RestartAsync(name);

            var logs = await p.GetLogsAsync(name, 10);
            Assert.NotNull(logs);
        }
        finally
        {
            await runner.RunAsync(exe, ["rm", "-f", name], CancellationToken.None);
        }
    }
}
