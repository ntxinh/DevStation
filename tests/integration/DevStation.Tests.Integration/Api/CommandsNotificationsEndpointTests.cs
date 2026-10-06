using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DevStation.Domain.Entities;
using DevStation.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace DevStation.Tests.Integration.Api;

public class CommandsNotificationsEndpointTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _pg =
        new PostgreSqlBuilder("docker.io/library/postgres:16-alpine").Build();
    private readonly WebApplicationFactory<Program> _factory;
    private HttpClient _admin = null!;
    private HttpClient _read = null!;

    public CommandsNotificationsEndpointTests()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
            b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Default"] = _pg.GetConnectionString(),
                    ["DevStation:Token"] = "test",
                    ["DevStation:TokenTiers:readtok"] = "read",
                })));
    }

    public async Task InitializeAsync()
    {
        await _pg.StartAsync();
        _admin = _factory.CreateClient();
        _admin.DefaultRequestHeaders.Add("Authorization", "Bearer test");
        _read = _factory.CreateClient();
        _read.DefaultRequestHeaders.Add("Authorization", "Bearer readtok");
    }

    public async Task DisposeAsync()
    {
        _admin.Dispose();
        _read.Dispose();
        await _factory.DisposeAsync();
        await _pg.DisposeAsync();
    }

    private DevStationDbContext NewDb()
    {
        var scope = _factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<DevStationDbContext>();
    }

    private static readonly JsonSerializerOptions Json =
        new() { PropertyNameCaseInsensitive = true };

    private record CommandRow(string Id, string Title, string Verb, string Target,
        string RequiredTier, bool NeedsConfirm);
    private record CommandsEnvelope(List<CommandRow> Data);
    private record ExecuteEnvelope(JsonElement Data);
    private record NotificationRow(Guid Id, string Type, string Title,
        string? Message, string? Link, bool Read, string? Severity);
    private record NotificationsEnvelope(List<NotificationRow> Data);
    private record AuditRow(string Action, string Result);
    private record AuditEnvelope(List<AuditRow> Data);

    [Fact]
    public async Task Get_commands_returns_nav_commands_for_admin_and_read()
    {
        var admin = await _admin.GetFromJsonAsync<CommandsEnvelope>("/api/commands", Json);
        Assert.NotNull(admin);
        Assert.Contains(admin!.Data, c => c is { Id: "nav.projects", Verb: "navigation", Target: "/projects", RequiredTier: "read" });

        var read = await _read.GetFromJsonAsync<CommandsEnvelope>("/api/commands", Json);
        Assert.NotNull(read);
        Assert.NotEmpty(read!.Data);
        Assert.All(read.Data, c => Assert.Equal("navigation", c.Verb));
    }

    [Fact]
    public async Task Execute_navigation_command_returns_route()
    {
        var res = await _admin.PostAsJsonAsync("/api/commands/nav.jobs/execute",
            new { confirm = false });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<ExecuteEnvelope>(Json);
        Assert.Equal("/jobs", body!.Data.GetProperty("route").GetString());
    }

    [Fact]
    public async Task Execute_requires_confirm_for_needsConfirm_commands()
    {
        await using var db = NewDb();
        var job = new JobDefinition { Name = "sweep", Type = "health-sweep" };
        db.JobDefinitions.Add(job);
        await db.SaveChangesAsync();

        var res = await _admin.PostAsJsonAsync($"/api/commands/job.trigger:{job.Id}/execute",
            new { confirm = false });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var body = await res.Content.ReadAsStringAsync();
        Assert.Contains("CONFIRMATION_REQUIRED", body);
    }

    [Fact]
    public async Task Execute_action_command_triggers_real_effect_and_audits()
    {
        Guid envId;
        await using (var db = NewDb())
        {
            var env = new AppEnvironment { Name = "staging", Type = "staging" };
            var proj = new Project { Name = "api" };
            db.Environments.Add(env);
            db.Projects.Add(proj);
            await db.SaveChangesAsync();
            envId = env.Id;
        }

        var id = $"deploy:staging:{envId}"; // resolved via list to prove materialization
        var cmds = await _admin.GetFromJsonAsync<CommandsEnvelope>("/api/commands", Json);
        var cmd = cmds!.Data.First(c => c.Id.StartsWith("deploy:", StringComparison.Ordinal));
        id = cmd.Id;

        var res = await _admin.PostAsJsonAsync($"/api/commands/{id}/execute",
            new { confirm = true, @params = new { commit = "abc123" } });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        await using var verify = NewDb();
        var dep = await verify.Deployments.FirstOrDefaultAsync(d => d.EnvironmentId == envId);
        Assert.NotNull(dep);
        Assert.Equal("abc123", dep!.Commit);
        var audit = await verify.AuditEvents
            .Where(a => a.Action == "command.execute" || a.Action == "deployment.start")
            .Select(a => new AuditRow(a.Action, a.Result)).ToListAsync();
        Assert.Contains(audit, a => a.Action == "command.execute" && a.Result == "ok");
        Assert.Contains(audit, a => a.Action == "deployment.start" && a.Result == "ok");
    }

    [Fact]
    public async Task Execute_denies_below_required_tier()
    {
        var res = await _read.PostAsJsonAsync("/api/commands/nav.dashboard/execute",
            new { confirm = false });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode); // read can run navigation

        await using var db = NewDb();
        var job = new JobDefinition { Name = "x", Type = "log-cleanup" };
        db.JobDefinitions.Add(job);
        await db.SaveChangesAsync();
        var denied = await _read.PostAsJsonAsync($"/api/commands/job.trigger:{job.Id}/execute",
            new { confirm = true });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    [Fact]
    public async Task Execute_unknown_command_is_404()
    {
        var res = await _admin.PostAsJsonAsync("/api/commands/nope/execute",
            new { confirm = true });
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Notifications_roundtrip_unread_read_readall()
    {
        Guid n1, n2;
        await using (var db = NewDb())
        {
            var a = new Notification { Type = "job_failed", Title = "sweep failed", Severity = "error" };
            var b = new Notification { Type = "health_unhealthy", Title = "api down" };
            db.Notifications.AddRange(a, b);
            await db.SaveChangesAsync();
            n1 = a.Id; n2 = b.Id;
        }

        var unread = await _admin.GetFromJsonAsync<NotificationsEnvelope>(
            "/api/notifications?unread=true", Json);
        Assert.Equal(2, unread!.Data.Count(n => n.Id == n1 || n.Id == n2));

        var mark = await _admin.PostAsync($"/api/notifications/{n1}/read", null);
        Assert.Equal(HttpStatusCode.OK, mark.StatusCode);

        var after = await _admin.GetFromJsonAsync<NotificationsEnvelope>(
            "/api/notifications?unread=true", Json);
        Assert.DoesNotContain(after!.Data, n => n.Id == n1);
        Assert.Contains(after.Data, n => n.Id == n2);

        var all = await _admin.PostAsync("/api/notifications/read-all", null);
        Assert.Equal(HttpStatusCode.OK, all.StatusCode);
        var none = await _admin.GetFromJsonAsync<NotificationsEnvelope>(
            "/api/notifications?unread=true", Json);
        Assert.Empty(none!.Data);
    }

    [Fact]
    public async Task Notification_read_unknown_id_is_404()
    {
        var res = await _admin.PostAsync($"/api/notifications/{Guid.NewGuid()}/read", null);
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }
}
