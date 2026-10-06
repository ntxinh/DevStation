using DevStation.Application.Modules.Commands.ExecuteCommand;
using DevStation.Application.Shared.Commands;
using DevStation.Application.Shared.Providers;
using DevStation.Domain.Entities;
using DevStation.Infrastructure.Notifications;
using DevStation.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DevStation.Tests.Unit.Commands;

public class CommandRegistryTests
{
    private sealed class FakeContainers(IReadOnlyList<ContainerInfo> list)
        : IContainerProvider
    {
        public Task<IReadOnlyList<ContainerInfo>> ListAsync(CancellationToken ct = default)
            => Task.FromResult(list);
        public Task<ContainerInspect> InspectAsync(string id, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<IReadOnlyList<string>> GetLogsAsync(string id, int tail, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<string>>([]);
        public Task<ContainerStats?> GetStatsAsync(string id, CancellationToken ct = default)
            => Task.FromResult<ContainerStats?>(null);
        public Task StartAsync(string id, CancellationToken ct = default) => Task.CompletedTask;
        public Task StopAsync(string id, CancellationToken ct = default) => Task.CompletedTask;
        public Task RestartAsync(string id, CancellationToken ct = default) => Task.CompletedTask;
    }

    private static DevStationDbContext NewDb() => new(
        new DbContextOptionsBuilder<DevStationDbContext>()
            .UseInMemoryDatabase("cmds-" + Guid.NewGuid()).Options);

    private static CommandRegistry NewRegistry(DevStationDbContext db,
        IReadOnlyList<ContainerInfo>? live = null)
        => new(db, new FakeContainers(live ?? []));

    private static DevStationDbContext SeededDb()
    {
        var db = NewDb();
        var p = new Project { Name = "web" };
        var dev = new AppEnvironment { Name = "dev", Type = "dev" };
        var prod = new AppEnvironment { Name = "prod", Type = "production" };
        var svc = new Service { Name = "api", ContainerId = "abc123" };
        var svcNoContainer = new Service { Name = "cron" };
        var job = new JobDefinition { Name = "sweep", Type = "health-sweep" };
        db.Projects.Add(p);
        db.Environments.AddRange(dev, prod);
        db.Services.AddRange(svc, svcNoContainer);
        db.JobDefinitions.Add(job);
        db.SaveChanges();
        return db;
    }

    [Fact]
    public async Task Read_tier_sees_navigation_commands_only()
    {
        var cmds = await NewRegistry(SeededDb()).ForTierAsync("read", CancellationToken.None);
        Assert.NotEmpty(cmds);
        Assert.All(cmds, c => Assert.Equal("navigation", c.Verb));
        Assert.Contains(cmds, c => c is { Id: "nav.dashboard", Target: "/dashboard" });
        Assert.Contains(cmds, c => c.Id.StartsWith("project.open:", StringComparison.Ordinal));
        Assert.DoesNotContain(cmds, c => c.Id.StartsWith("deploy:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Admin_sees_navigation_and_action_commands()
    {
        var cmds = await NewRegistry(SeededDb()).ForTierAsync("admin", CancellationToken.None);
        Assert.Contains(cmds, c => c.Verb == "navigation");
        Assert.Contains(cmds, c => c.Id.StartsWith("service.restart:", StringComparison.Ordinal));
        Assert.Contains(cmds, c => c.Id.StartsWith("job.trigger:", StringComparison.Ordinal));
        Assert.Contains(cmds, c => c.Id.StartsWith("deploy:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Execute_tier_hides_production_deploy()
    {
        var cmds = await NewRegistry(SeededDb()).ForTierAsync("execute", CancellationToken.None);
        var deploys = cmds.Where(c => c.Id.StartsWith("deploy:", StringComparison.Ordinal)).ToList();
        Assert.Single(deploys);
        Assert.Equal("execute", deploys[0].RequiredTier);
        Assert.All(deploys, c => Assert.DoesNotContain("prod", c.Title));
    }

    [Fact]
    public async Task Restart_service_only_when_container_mapped()
    {
        var cmds = await NewRegistry(SeededDb()).ForTierAsync("admin", CancellationToken.None);
        var restarts = cmds.Where(c => c.Id.StartsWith("service.restart:", StringComparison.Ordinal)).ToList();
        Assert.Single(restarts);
        Assert.Equal("Restart service api", restarts[0].Title);
        Assert.True(restarts[0].NeedsConfirm);
    }

    [Fact]
    public async Task Unknown_tier_sees_nothing()
    {
        var cmds = await NewRegistry(SeededDb()).ForTierAsync("bogus", CancellationToken.None);
        Assert.Empty(cmds);
    }

    [Fact]
    public async Task Container_commands_come_from_live_provider()
    {
        var live = new List<ContainerInfo>
        {
            new("c1", "web-app", "img", "running", "Up", []),
        };
        var cmds = await NewRegistry(NewDb(), live).ForTierAsync("admin", CancellationToken.None);
        Assert.Contains(cmds, c => c is { Id: "container.open:c1", Verb: "navigation", Target: "/containers/c1" });
        Assert.Contains(cmds, c => c is { Id: "container.restart:c1", Verb: "action" });
    }

    [Fact]
    public async Task FindAsync_resolves_materialized_command()
    {
        var db = SeededDb();
        var reg = NewRegistry(db);
        var all = await reg.AllAsync(CancellationToken.None);
        var pick = all.First(c => c.Verb == "action");
        Assert.Equal(pick.Id, (await reg.FindAsync(pick.Id, CancellationToken.None))?.Id);
        Assert.Null(await reg.FindAsync("nope", CancellationToken.None));
    }

    [Theory]
    [InlineData("service.restart:3fa85f64-5717-4562-b3fc-2c963f66afa6", "service.restart", 1)]
    [InlineData("deploy:3fa85f64-5717-4562-b3fc-2c963f66afa6:3fa85f64-5717-4562-b3fc-2c963f66afa7", "deploy", 2)]
    public void Action_targets_parse_into_verb_and_args(string target, string action, int args)
    {
        var parts = ExecuteCommandHandler.SplitTarget(target);
        Assert.Equal(action, parts[0]);
        Assert.Equal(args, parts.Length - 1);
    }

    [Fact]
    public void Notification_metadata_redacts_sensitive_keys()
    {
        var json = NotificationService.RedactMetadata(
            new { jobName = "sweep", apiToken = "abc", nested = new { Password = "x" } });
        Assert.NotNull(json);
        Assert.Contains("\"***\"", json);
        Assert.Contains("sweep", json);
        Assert.DoesNotContain("abc", json);
        Assert.DoesNotContain("\"x\"", json);
        Assert.Null(NotificationService.RedactMetadata(null));
    }
}
