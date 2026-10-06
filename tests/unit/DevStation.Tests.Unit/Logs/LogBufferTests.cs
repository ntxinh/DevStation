using DevStation.Application.Shared.Logging;
using DevStation.Infrastructure.Logs;
using Microsoft.Extensions.DependencyInjection;

namespace DevStation.Tests.Unit.Logs;

public class LogBufferTests
{
    private static LogEntry E(string msg, string level = "info",
        string source = "api", Guid? env = null,
        Dictionary<string, string>? meta = null)
        => new(DateTimeOffset.UtcNow, level, source, env, msg, meta);

    private sealed class FakeNotifier : ILogNotifier
    {
        public TaskCompletionSource<LogEntry> Received { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task Publish(LogEntry e, CancellationToken ct = default)
        {
            Received.TrySetResult(e);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public void Query_returns_newest_first()
    {
        var buf = new LogBuffer();
        buf.Write(E("first"));
        Thread.Sleep(2); // distinct timestamps
        buf.Write(E("second"));
        var rows = buf.Query(new LogFilter());
        Assert.Equal("second", rows[0].Message);
        Assert.Equal("first", rows[1].Message);
    }

    [Fact]
    public void Cap_evicts_oldest_entries()
    {
        var buf = new LogBuffer(cap: 3);
        for (var i = 0; i < 5; i++) buf.Write(E($"m-{i}"));
        Assert.Equal(3, buf.Count);
        var rows = buf.Query(new LogFilter());
        Assert.Equal(["m-4", "m-3", "m-2"], rows.Select(r => r.Message).ToArray());
    }

    [Fact]
    public void Query_filters_by_source_severity_environment_and_text()
    {
        var env = Guid.NewGuid();
        var other = Guid.NewGuid();
        var buf = new LogBuffer();
        buf.Write(E("deploy ok", "info", "deployment", env));
        buf.Write(E("api boom", "error", "api", env));
        buf.Write(E("other env boom", "error", "api", other));
        buf.Write(E("deploy boom", "error", "deployment", env));

        Assert.Empty(buf.Query(new LogFilter(Source: "container")));

        var bySource = buf.Query(new LogFilter(Source: "deployment"));
        Assert.Equal(2, bySource.Count);
        Assert.All(bySource, r => Assert.Equal("deployment", r.Source));

        var bySev = buf.Query(new LogFilter(Severity: "error"));
        Assert.Equal(3, bySev.Count);

        var byEnv = buf.Query(new LogFilter(EnvironmentId: env));
        Assert.Equal(3, byEnv.Count);

        var byText = buf.Query(new LogFilter(Q: "boom"));
        Assert.Equal(3, byText.Count);
        Assert.All(byText, r => Assert.Contains("boom", r.Message));

        var combined = buf.Query(new LogFilter(Source: "deployment",
            Severity: "error", EnvironmentId: env, Q: "boom"));
        Assert.Single(combined);
        Assert.Equal("deploy boom", combined[0].Message);
    }

    [Fact]
    public void Query_respects_take()
    {
        var buf = new LogBuffer();
        for (var i = 0; i < 10; i++) buf.Write(E($"m-{i}"));
        Assert.Equal(4, buf.Query(new LogFilter(Take: 4)).Count);
    }

    [Fact]
    public void ForDeployment_returns_only_matching_entries()
    {
        var depId = Guid.NewGuid();
        var buf = new LogBuffer();
        buf.Write(E("mine", "info", "deployment", null,
            new() { ["deploymentId"] = depId.ToString() }));
        buf.Write(E("other dep", "info", "deployment", null,
            new() { ["deploymentId"] = Guid.NewGuid().ToString() }));
        buf.Write(E("no meta"));

        var rows = buf.ForDeployment(depId);
        Assert.Single(rows);
        Assert.Equal("mine", rows[0].Message);
    }

    [Fact]
    public async Task Write_broadcasts_entry_to_notifier()
    {
        var notifier = new FakeNotifier();
        var sp = new ServiceCollection()
            .AddSingleton<ILogNotifier>(notifier)
            .BuildServiceProvider();
        var buf = new LogBuffer(sp);
        buf.Write(E("live line"));
        var e = await notifier.Received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("live line", e.Message);
    }
}
