using DevStation.Application.Shared.Providers;
using DevStation.Domain.Enums;

namespace DevStation.Tests.Unit.Jobs;

public class JobStatusDeriverTests
{
    private static readonly DateTimeOffset Ran = DateTimeOffset.UtcNow;

    [Fact]
    public void Paused_wins_over_everything() =>
        Assert.Equal("paused",
            JobStatusDeriver.Derive(true, Ran, JobStatus.Failed));

    [Fact]
    public void No_runs_is_never_run() =>
        Assert.Equal("never-run",
            JobStatusDeriver.Derive(false, null, null));

    [Fact]
    public void Last_failed_is_failing() =>
        Assert.Equal("failing",
            JobStatusDeriver.Derive(false, Ran, JobStatus.Failed));

    [Fact]
    public void Last_succeeded_is_healthy() =>
        Assert.Equal("healthy",
            JobStatusDeriver.Derive(false, Ran, JobStatus.Succeeded));

    [Fact]
    public void Unpaused_with_runs_and_idle_is_healthy() =>
        Assert.Equal("healthy",
            JobStatusDeriver.Derive(false, Ran, JobStatus.Idle));
}
