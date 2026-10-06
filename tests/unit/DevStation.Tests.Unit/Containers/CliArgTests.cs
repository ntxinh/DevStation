using DevStation.Application.Shared.Providers;
using DevStation.Application.Shared.Results;
using DevStation.Infrastructure.Containers;

namespace DevStation.Tests.Unit.Containers;

/// <summary>Captures the argv the provider would hand to ProcessStartInfo.ArgumentList —
/// the assertion is "no shell string ever exists".</summary>
public sealed class RecordingRunner : ContainerCliRunner
{
    public List<string[]> Calls { get; } = [];
    public string Stdout { get; set; } = "";

    public override Task<string> RunAsync(string exe, IReadOnlyList<string> args,
        CancellationToken ct = default)
    {
        Calls.Add([exe, .. args]);
        return Task.FromResult(Stdout);
    }
}

public sealed class CliArgTests
{
    [Theory]
    [InlineData("a1b2c3d4e5f6")]
    [InlineData("web")]
    [InlineData("my-container_1.2")]
    [InlineData("abcDEF012")]
    public void Ensure_accepts_valid_ids(string id)
        => Assert.Equal(id, ContainerId.Ensure(id));

    [Fact]
    public void Ensure_accepts_128_chars()
        => ContainerId.Ensure(new string('a', 128));

    [Theory]
    [InlineData("; rm -rf /")]
    [InlineData("--privileged")]
    [InlineData("with space")]
    [InlineData("a;b")]
    [InlineData("a|b")]
    [InlineData("$(id)")]
    [InlineData("`id`")]
    [InlineData("-leadingdash")]
    [InlineData(".leadingdot")]
    [InlineData("")]
    public void Ensure_rejects_hostile_or_malformed_ids(string id)
    {
        var ex = Assert.Throws<AppException>(() => ContainerId.Ensure(id));
        Assert.Equal("CONTAINER_ID_INVALID", ex.Code);
        Assert.Equal(400, ex.Status);
    }

    [Fact]
    public void Ensure_rejects_overlong_id()
        => Assert.Throws<AppException>(() => ContainerId.Ensure(new string('a', 129)));

    [Theory]
    [InlineData("start")]
    [InlineData("stop")]
    [InlineData("restart")]
    public async Task Actions_never_invoke_cli_on_invalid_id(string action)
    {
        var runner = new RecordingRunner();
        var p = new PodmanCliProvider(runner);
        // sync-validated before a Task exists — invoke lazily inside the assertion
        var ex = await Assert.ThrowsAsync<AppException>(() => action switch
        {
            "start" => p.StartAsync("bad id; rm -rf"),
            "stop" => p.StopAsync("bad id; rm -rf"),
            _ => p.RestartAsync("bad id; rm -rf"),
        });
        Assert.Equal("CONTAINER_ID_INVALID", ex.Code);
        Assert.Empty(runner.Calls);
    }

    [Fact]
    public async Task Inspect_never_invokes_cli_on_invalid_id()
    {
        var runner = new RecordingRunner();
        await Assert.ThrowsAsync<AppException>(
            () => new DockerCliProvider(runner).InspectAsync("--privileged"));
        Assert.Empty(runner.Calls);
    }

    [Fact]
    public async Task Actions_pass_verbatim_argv_no_shell()
    {
        var runner = new RecordingRunner();
        var p = new PodmanCliProvider(runner);
        await p.RestartAsync("web_1");
        Assert.Equal(["podman", "restart", "web_1"], runner.Calls[0]);
    }

    [Fact]
    public async Task Logs_tail_is_clamped_and_timestamps_on()
    {
        var runner = new RecordingRunner();
        var p = new PodmanCliProvider(runner);
        await p.GetLogsAsync("c1", 99999);
        Assert.Equal(["podman", "logs", "--timestamps", "--tail", "5000", "c1"],
            runner.Calls[0]);
        await p.GetLogsAsync("c1", 0);
        Assert.Equal(["podman", "logs", "--timestamps", "--tail", "1", "c1"],
            runner.Calls[1]);
        await p.GetLogsAsync("c1", 50);
        Assert.Equal(["podman", "logs", "--timestamps", "--tail", "50", "c1"],
            runner.Calls[2]);
    }

    [Fact]
    public async Task List_uses_ps_format_json()
    {
        var runner = new RecordingRunner { Stdout = "[]" };
        var p = new PodmanCliProvider(runner);
        var list = await p.ListAsync();
        Assert.Empty(list);
        Assert.Equal(["podman", "ps", "-a", "--format", "json"], runner.Calls[0]);
    }

    [Fact]
    public async Task Docker_provider_uses_docker_exe()
    {
        var runner = new RecordingRunner { Stdout = "[]" };
        var p = new DockerCliProvider(runner);
        await p.ListAsync();
        Assert.Equal("docker", runner.Calls[0][0]);
    }
}
