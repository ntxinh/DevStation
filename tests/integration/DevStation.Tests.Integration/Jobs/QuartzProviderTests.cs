using DevStation.Application.Shared.Auditing;
using DevStation.Application.Shared.Persistence;
using DevStation.Application.Shared.Providers;
using DevStation.Domain.Entities;
using DevStation.Domain.Enums;
using DevStation.Infrastructure.Jobs;
using DevStation.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using Testcontainers.PostgreSql;

namespace DevStation.Tests.Integration.Jobs;

/// <summary>Real Quartz scheduler (RAMJobStore) + real Postgres: trigger a job,
/// assert the listener wrote a job_executions row; pause → next run cleared.</summary>
public class QuartzProviderTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _pg =
        new PostgreSqlBuilder("docker.io/library/postgres:16-alpine").Build();
    private ServiceProvider _sp = null!;
    private IScheduler _scheduler = null!;

    public async Task InitializeAsync()
    {
        await _pg.StartAsync();

        var regs = new List<JobRegistration>
        {
            new()
            {
                Name = "it-sweep", Type = "health-sweep",
                // once a year — never fires on its own during the test
                Cron = "0 0 0 1 1 ?", Timezone = "UTC",
            },
        };
        var s = new ServiceCollection();
        s.AddLogging();
        s.AddDbContext<DevStationDbContext>(o =>
            o.UseNpgsql(_pg.GetConnectionString()));
        s.AddScoped<IDevStationDb>(x => x.GetRequiredService<DevStationDbContext>());
        s.AddScoped<IAuditWriter, FakeAudit>();
        s.AddSingleton<IHealthCheckExecutor, NoopExecutor>();
        s.AddSingleton<IReadOnlyList<JobRegistration>>(regs);
        s.AddSingleton<IJobProvider, QuartzJobProvider>();
        s.AddQuartz(q =>
        {
            q.AddJobListener(x => new AuditingJobListener(
                x.GetRequiredService<IServiceScopeFactory>(),
                x.GetRequiredService<Microsoft.Extensions.Logging.ILogger<AuditingJobListener>>()),
                []);
            foreach (var reg in regs)
            {
                q.AddJob(reg.JobType, j => j.WithIdentity(reg.Name));
                q.AddTrigger(t => t
                    .WithIdentity($"{reg.Name}-trigger")
                    .ForJob(reg.Name)
                    .WithSchedule(reg.Schedule));
            }
        });
        _sp = s.BuildServiceProvider();
        _scheduler = await _sp.GetRequiredService<ISchedulerFactory>().GetScheduler();
        await _scheduler.Start();

        var opt = new DbContextOptionsBuilder<DevStationDbContext>()
            .UseNpgsql(_pg.GetConnectionString()).Options;
        await using var db = new DevStationDbContext(opt);
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _scheduler.Shutdown();
        await _sp.DisposeAsync();
        await _pg.DisposeAsync();
    }

    private async Task<DevStationDbContext> NewDb()
    {
        var scope = _sp.CreateAsyncScope();
        return scope.ServiceProvider.GetRequiredService<DevStationDbContext>();
    }

    private static async Task<T> Poll<T>(Func<Task<T?>> probe,
        Func<T?, bool> done, int tries = 60)
    {
        for (var i = 0; i < tries; i++)
        {
            var v = await probe();
            if (done(v)) return v!;
            await Task.Delay(250);
        }
        throw new TimeoutException("condition not met in time");
    }

    [Fact]
    public async Task Trigger_runs_job_and_records_execution_row()
    {
        var jobs = _sp.GetRequiredService<IJobProvider>();
        var db = await NewDb();
        var def = db.JobDefinitions.Add(new JobDefinition
        { Name = "it-sweep", Type = "health-sweep" });
        await db.SaveChangesAsync();

        await jobs.TriggerAsync(def.Entity.Id, CancellationToken.None);

        var ex = await Poll(async () =>
        {
            var db = await NewDb();
            return await db.JobExecutions
                .Where(e => e.JobDefinitionId == def.Entity.Id)
                .OrderByDescending(e => e.StartedAt)
                .FirstOrDefaultAsync();
        }, e => e is not null);

        Assert.Equal(JobStatus.Succeeded, ex.Status);
        Assert.True(ex.DurationMs >= 0);
        Assert.NotNull(ex.EndedAt);
    }

    [Fact]
    public async Task Pause_clears_next_run_and_resume_restores_it()
    {
        var jobs = _sp.GetRequiredService<IJobProvider>();
        var db = await NewDb();
        var def = db.JobDefinitions.Add(new JobDefinition
        { Name = "it-sweep", Type = "health-sweep" });
        await db.SaveChangesAsync();

        var before = await jobs.GetAsync(def.Entity.Id, CancellationToken.None);
        Assert.Equal("never-run", before!.Status);
        Assert.NotNull(before.NextRun);

        await jobs.PauseAsync(def.Entity.Id, CancellationToken.None);
        var paused = await jobs.GetAsync(def.Entity.Id, CancellationToken.None);
        Assert.Equal("paused", paused!.Status);
        Assert.Null(paused.NextRun);

        await jobs.ResumeAsync(def.Entity.Id, CancellationToken.None);
        var resumed = await jobs.GetAsync(def.Entity.Id, CancellationToken.None);
        Assert.Equal("never-run", resumed!.Status);
        Assert.NotNull(resumed.NextRun);
    }

    private sealed class FakeAudit : IAuditWriter
    {
        public Task Record(string action, string resource, Guid? resourceId,
            string? user, string result, object? metadata, CancellationToken ct = default)
            => Task.CompletedTask;
    }

    private sealed class NoopExecutor : IHealthCheckExecutor
    {
        public Task<HealthResult> Execute(HealthCheckDef check, CancellationToken ct)
            => Task.FromResult(new HealthResult(HealthStatus.Healthy, 1, null,
                DateTimeOffset.UtcNow));
    }
}
