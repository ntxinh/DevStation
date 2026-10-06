using DevStation.Application.Shared.Auditing;
using DevStation.Application.Shared.Persistence;
using DevStation.Application.Shared.Providers;
using DevStation.Domain.Entities;
using DevStation.Domain.Enums;
using DevStation.Infrastructure.Jobs;
using DevStation.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DevStation.Tests.Unit.Jobs;

public class AuditingJobListenerTests
{
    private sealed class FakeAudit : IAuditWriter
    {
        public List<(string Action, string Result)> Rows { get; } = [];
        public Task Record(string action, string resource, Guid? resourceId,
            string? user, string result, object? metadata, CancellationToken ct = default)
        {
            Rows.Add((action, result));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeNotifier : IJobNotifier
    {
        public List<JobStatusUpdate> Updates { get; } = [];
        public Task Publish(JobStatusUpdate update, CancellationToken ct)
        {
            Updates.Add(update);
            return Task.CompletedTask;
        }
    }

    private static (AuditingJobListener listener, ServiceProvider root) Make(
        Action<IServiceCollection>? extra = null)
    {
        // shared DbContextOptions → one in-memory store across scopes;
        // AddDbContext+UseInMemoryDatabase proven flaky here (per-scope stores)
        var opt = new DbContextOptionsBuilder<DevStationDbContext>()
            .UseInMemoryDatabase("listener-" + Guid.NewGuid()).Options;
        var s = new ServiceCollection()
            .AddSingleton(opt)
            .AddScoped<DevStationDbContext>(x => new DevStationDbContext(
                x.GetRequiredService<DbContextOptions<DevStationDbContext>>()))
            .AddScoped<IDevStationDb>(x => x.GetRequiredService<DevStationDbContext>())
            .AddSingleton<IAuditWriter, FakeAudit>();
        extra?.Invoke(s);
        var root = s.BuildServiceProvider();
        return (new AuditingJobListener(root.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<AuditingJobListener>.Instance), root);
    }

    private static async Task<DevStationDbContext> Db(ServiceProvider root)
    {
        var scope = root.CreateAsyncScope();
        return scope.ServiceProvider.GetRequiredService<DevStationDbContext>();
    }

    [Fact]
    public async Task Success_records_execution_bumps_counters_audits_and_notifies()
    {
        var notifier = new FakeNotifier();
        var (listener, root) = Make(s => s.AddSingleton<IJobNotifier>(notifier));

        await listener.Record("health-sweep", typeof(HealthSweepJob),
            new DateTimeOffset(2026, 10, 6, 1, 0, 0, TimeSpan.Zero),
            TimeSpan.FromMilliseconds(120), error: null, CancellationToken.None);

        var db = await Db(root);
        var def = await db.JobDefinitions.SingleAsync(d => d.Name == "health-sweep");
        Assert.Equal("health-sweep", def.Type);
        Assert.Equal(JobStatus.Succeeded, def.Status);
        Assert.Equal(1, def.SuccessCount);
        Assert.Equal(0, def.FailureCount);
        Assert.NotNull(def.LastRunAt);

        var ex = await db.JobExecutions.SingleAsync();
        Assert.Equal(def.Id, ex.JobDefinitionId);
        Assert.Equal(JobStatus.Succeeded, ex.Status);
        Assert.Equal(120, ex.DurationMs);
        Assert.Null(ex.Error);
        Assert.Equal(new DateTimeOffset(2026, 10, 6, 1, 0, 0, TimeSpan.Zero)
            .AddMilliseconds(120), ex.EndedAt);

        var audit = (FakeAudit)root.GetRequiredService<IAuditWriter>();
        Assert.Equal(("job.executed", "ok"), audit.Rows.Single());
        Assert.Equal("healthy", notifier.Updates.Single().Status);
        Assert.Equal(def.Id, notifier.Updates.Single().JobId);
    }

    [Fact]
    public async Task Failure_marks_def_failing_and_truncates_error()
    {
        var (listener, root) = Make();

        await listener.Record("log-cleanup", typeof(LogCleanupJob),
            DateTimeOffset.UtcNow, TimeSpan.FromMilliseconds(5),
            new string('x', 5000), CancellationToken.None);

        var db = await Db(root);
        var def = await db.JobDefinitions.SingleAsync(d => d.Name == "log-cleanup");
        Assert.Equal(JobStatus.Failed, def.Status);
        Assert.Equal(1, def.FailureCount);
        var ex = await db.JobExecutions.SingleAsync();
        Assert.Equal(JobStatus.Failed, ex.Status);
        Assert.Equal(4000, ex.Error!.Length);

        var audit = (FakeAudit)root.GetRequiredService<IAuditWriter>();
        Assert.Equal("error", audit.Rows.Single().Result);
    }

    [Fact]
    public async Task Second_run_accumulates_counters()
    {
        var (listener, root) = Make();
        var now = DateTimeOffset.UtcNow;

        await listener.Record("log-cleanup", typeof(LogCleanupJob), now,
            TimeSpan.FromMilliseconds(1), null, CancellationToken.None);
        await listener.Record("log-cleanup", typeof(LogCleanupJob), now,
            TimeSpan.FromMilliseconds(2), "boom", CancellationToken.None);

        var db = await Db(root);
        var def = await db.JobDefinitions.SingleAsync(d => d.Name == "log-cleanup");
        Assert.Equal(1, def.SuccessCount);
        Assert.Equal(1, def.FailureCount);
        Assert.Equal(JobStatus.Failed, def.Status);
        Assert.Equal(2, await db.JobExecutions.CountAsync());
    }
}
