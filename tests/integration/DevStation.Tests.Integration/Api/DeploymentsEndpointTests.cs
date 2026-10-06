using System.Net;
using System.Net.Http.Json;
using DevStation.Domain.Entities;
using DevStation.Domain.Enums;
using DevStation.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using DevStation.Application.Shared.Providers;
using Testcontainers.PostgreSql;

namespace DevStation.Tests.Integration.Api;

public class DeploymentsEndpointTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _pg =
        new PostgreSqlBuilder("docker.io/library/postgres:16-alpine").Build();
    private readonly WebApplicationFactory<Program> _factory;
    private HttpClient _client = null!;
    private HttpClient _executeClient = null!;

    public DeploymentsEndpointTests()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
            b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = _pg.GetConnectionString(),
                ["DevStation:Token"] = "test",
                // second token at Execute tier for prod-vs-dev policy coverage
                ["DevStation:TokenTiers:exec-token"] = "execute",
                // listed-but-empty tier must fail-safe to read, not admin
                ["DevStation:TokenTiers:empty-tier-token"] = "",
                // keep deployments queued ~5s so the cancel test has a window;
                // progression test waits up to 10s
                ["Deployments:Mock:QueueSeconds"] = "5",
                ["Deployments:Mock:RunSeconds"] = "0",
            })));
    }

    public async Task InitializeAsync()
    {
        await _pg.StartAsync();
        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Add("Authorization", "Bearer test");
        _executeClient = _factory.CreateClient();
        _executeClient.DefaultRequestHeaders.Add("Authorization", "Bearer exec-token");
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        _executeClient.Dispose();
        await _factory.DisposeAsync();
        await _pg.DisposeAsync();
    }

    private DevStationDbContext NewDb()
    {
        var scope = _factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<DevStationDbContext>();
    }

    private async Task<Guid> AddEnv(string type)
    {
        await using var db = NewDb();
        var e = new AppEnvironment { Name = $"env-{Guid.NewGuid():N}", Type = type };
        db.Environments.Add(e);
        await db.SaveChangesAsync();
        return e.Id;
    }

    private record DeploymentDto(Guid Id, string? ProjectName, string? EnvironmentName,
        string? Commit, string Status, long? DurationMs,
        DateTimeOffset? StartedAt, DateTimeOffset? FinishedAt,
        List<string> Logs);

    private record EnvEnvelope(DeploymentDto Data);
    private record ListEnvelope(List<DeploymentDto> Data);
    private record ErrBody(object? Data, ErrDetail Error);
    private record ErrDetail(string Code, string Message);

    private async Task<Guid> WaitForStatusAsync(Guid id, int ms = 10000,
        params string[] statuses)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(ms);
        while (DateTime.UtcNow < deadline)
        {
            await using var db = NewDb();
            var row = await db.Deployments.FindAsync(id);
            if (row is not null &&
                statuses.Contains(row.Status.ToString().ToLowerInvariant()))
                return id;
            await Task.Delay(100);
        }
        await using var db2 = NewDb();
        var final = await db2.Deployments.FindAsync(id);
        Assert.Fail($"deployment {id} stuck at {final?.Status}");
        return id;
    }

    [Fact]
    public async Task Post_creates_row_and_progresses_to_succeeded()
    {
        var envId = await AddEnv("dev");

        var res = await _client.PostAsJsonAsync(
            $"/api/deployments?confirm=true&environmentId={envId}",
            new { projectId = (Guid?)null, commit = "deadbeef" });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<EnvEnvelope>();
        Assert.Equal("queued", body!.Data.Status);

        await WaitForStatusAsync(body.Data.Id, 10000, "succeeded", "failed");

        await using var db = NewDb();
        var row = await db.Deployments.FindAsync(body.Data.Id);
        Assert.Equal(DeploymentStatus.Succeeded, row!.Status);
        Assert.Equal("deadbeef", row.Commit);
        Assert.Equal(envId, row.EnvironmentId);

        var detail = await _client.GetFromJsonAsync<EnvEnvelope>(
            $"/api/deployments/{body.Data.Id}");
        Assert.Equal("succeeded", detail!.Data.Status);
        Assert.NotEmpty(detail.Data.Logs);

        var envName = (await db.Environments.FindAsync(envId))!.Name;
        var list = await _client.GetFromJsonAsync<ListEnvelope>("/api/deployments");
        Assert.Contains(list!.Data, d => d.Id == body.Data.Id &&
            d.EnvironmentName == envName);
    }

    [Fact]
    public async Task Post_without_confirm_returns_400()
    {
        var envId = await AddEnv("dev");
        var res = await _client.PostAsJsonAsync(
            $"/api/deployments?environmentId={envId}", new { });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var err = await res.Content.ReadFromJsonAsync<ErrBody>();
        Assert.Equal("CONFIRMATION_REQUIRED", err!.Error.Code);
    }

    [Fact]
    public async Task Prod_deploy_requires_deploy_tier()
    {
        var prodId = await AddEnv("production");
        // execute-tier token: confirm provided, still 403 on prod env
        var res = await _executeClient.PostAsJsonAsync(
            $"/api/deployments?confirm=true&environmentId={prodId}", new { });
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);

        // admin token passes policy; deploy proceeds
        var ok = await _client.PostAsJsonAsync(
            $"/api/deployments?confirm=true&environmentId={prodId}", new { });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);

        // dev env: execute-tier token is enough
        var devId = await AddEnv("dev");
        var dev = await _executeClient.PostAsJsonAsync(
            $"/api/deployments?confirm=true&environmentId={devId}", new { });
        Assert.Equal(HttpStatusCode.OK, dev.StatusCode);
    }

    [Fact]
    public async Task Cancel_running_deployment_marks_cancelled_second_cancel_409()
    {
        var envId = await AddEnv("dev");

        var res = await _client.PostAsJsonAsync(
            $"/api/deployments?confirm=true&environmentId={envId}", new { });
        var created = await res.Content.ReadFromJsonAsync<EnvEnvelope>();
        Assert.NotNull(created);

        var cancel = await _client.PostAsync(
            $"/api/deployments/{created.Data.Id}/cancel", null);
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        var cancelled = await cancel.Content.ReadFromJsonAsync<EnvEnvelope>();
        Assert.Equal("cancelled", cancelled!.Data.Status);

        var again = await _client.PostAsync(
            $"/api/deployments/{created.Data.Id}/cancel", null);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public void Deployment_notifier_is_registered_for_hub_broadcasts()
        => Assert.NotNull(_factory.Services.GetService<IDeploymentNotifier>());

    [Fact]
    public async Task Empty_tier_token_fails_safe_to_read()
    {
        using var c = _factory.CreateClient();
        c.DefaultRequestHeaders.Add("Authorization", "Bearer empty-tier-token");

        // read-tier works
        var list = await c.GetAsync("/api/deployments");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);

        // but can't execute — cancel needs Execute
        var cancel = await c.PostAsync($"/api/deployments/{Guid.NewGuid()}/cancel", null);
        Assert.Equal(HttpStatusCode.Forbidden, cancel.StatusCode);
    }
}
