using DevStation.Application.Shared.Providers;
using DevStation.Application.Shared.Results;
using DevStation.Domain.Enums;
using DevStation.Infrastructure.Deployments;
using DevStation.Infrastructure.Logging;
using DevStation.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DevStation.Tests.Unit.Deployments;

public class MockProviderTests
{
    private static MockDeploymentProvider NewProvider(InMemoryLogSink sink,
        DevStationDbContext? db, string name)
    {
        var services = new ServiceCollection();
        if (db is not null)
            services.AddScoped<DevStation.Application.Shared.Persistence.IDevStationDb>(
                _ => new DevStationDbContext(
                    new DbContextOptionsBuilder<DevStationDbContext>()
                        .UseInMemoryDatabase(name).Options));
        var sp = services.BuildServiceProvider();
        return new MockDeploymentProvider(sp.GetRequiredService<IServiceScopeFactory>(),
            sink, options: new MockDeploymentOptions { QueueSeconds = 0, RunSeconds = 0 });
    }

    private static async Task<DeploymentStatusInfo> WaitFor(
        IDeploymentProvider p, Guid id,
        Func<DeploymentStatus, bool> done, int ms = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(ms);
        while (DateTime.UtcNow < deadline)
        {
            var s = await p.GetStatusAsync(id, CancellationToken.None);
            if (done(s.Status)) return s;
            await Task.Delay(10);
        }
        return await p.GetStatusAsync(id, CancellationToken.None);
    }

    [Fact]
    public async Task Start_creates_row_queued_then_progresses_to_succeeded()
    {
        var sink = new InMemoryLogSink();
        var db = new DevStationDbContext(new DbContextOptionsBuilder<DevStationDbContext>()
            .UseInMemoryDatabase("mockdep1").Options);
        var p = NewProvider(sink, db, "mockdep1");

        var id = await p.StartAsync(null, Guid.NewGuid(), "abc123", "dev-user", CancellationToken.None);

        var status = await p.GetStatusAsync(id, CancellationToken.None);
        Assert.Equal(DeploymentStatus.Queued, status.Status);
        Assert.Equal("abc123", status.Commit);

        var final = await WaitFor(p, id, s => s is DeploymentStatus.Succeeded or DeploymentStatus.Failed);
        Assert.Equal(DeploymentStatus.Succeeded, final.Status); // failRate 0
        Assert.NotNull(final.StartedAt);
        Assert.NotNull(final.FinishedAt);
        Assert.NotEmpty(final.Logs);

        // dual-write: db row matches
        var row = await db.Deployments.FindAsync(id);
        Assert.NotNull(row);
        Assert.Equal(DeploymentStatus.Succeeded, row!.Status);
        Assert.NotNull(row.FinishedAt);
        Assert.Equal("dev-user", row.TriggeredBy); // survives running transition persist

        // log sink captured lines tagged with deploymentId
        var entries = sink.ForDeployment(id);
        Assert.NotEmpty(entries);
        Assert.All(entries, e => Assert.Equal("deployment", e.Source));
    }

    [Fact]
    public async Task FailRate_one_always_fails()
    {
        var sink = new InMemoryLogSink();
        var sp = new ServiceCollection().BuildServiceProvider();
        var p = new MockDeploymentProvider(sp.GetRequiredService<IServiceScopeFactory>(),
            sink, options: new MockDeploymentOptions { QueueSeconds = 0, RunSeconds = 0, FailRate = 1 });

        var id = await p.StartAsync(null, null, null, null, CancellationToken.None);
        var final = await WaitFor(p, id, s => s == DeploymentStatus.Failed);
        Assert.Equal(DeploymentStatus.Failed, final.Status);
    }

    [Fact]
    public async Task Cancel_queued_or_running_marks_cancelled()
    {
        var sink = new InMemoryLogSink();
        var sp = new ServiceCollection().BuildServiceProvider();
        var p = new MockDeploymentProvider(sp.GetRequiredService<IServiceScopeFactory>(),
            sink,
            options: new MockDeploymentOptions { QueueSeconds = 60, RunSeconds = 60 });

        var id = await p.StartAsync(null, null, null, null, CancellationToken.None);
        await p.CancelAsync(id, CancellationToken.None);

        var s = await p.GetStatusAsync(id, CancellationToken.None);
        Assert.Equal(DeploymentStatus.Cancelled, s.Status);
        Assert.NotNull(s.FinishedAt);
    }

    [Fact]
    public async Task Cancel_finished_throws_409()
    {
        var sink = new InMemoryLogSink();
        var sp = new ServiceCollection().BuildServiceProvider();
        var p = new MockDeploymentProvider(sp.GetRequiredService<IServiceScopeFactory>(),
            sink, options: new MockDeploymentOptions { QueueSeconds = 0, RunSeconds = 0 });

        var id = await p.StartAsync(null, null, null, null, CancellationToken.None);
        await WaitFor(p, id, s => s == DeploymentStatus.Succeeded);

        var ex = await Assert.ThrowsAsync<AppException>(
            () => p.CancelAsync(id, CancellationToken.None));
        Assert.Equal(409, ex.Status);
    }

    [Fact]
    public async Task Cancel_unknown_throws_404()
    {
        var sink = new InMemoryLogSink();
        var sp = new ServiceCollection().BuildServiceProvider();
        var p = new MockDeploymentProvider(sp.GetRequiredService<IServiceScopeFactory>(), sink);

        var ex = await Assert.ThrowsAsync<AppException>(
            () => p.CancelAsync(Guid.NewGuid(), CancellationToken.None));
        Assert.Equal(404, ex.Status);
    }
}
