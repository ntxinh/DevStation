using System.Net;
using System.Net.Http.Json;
using DevStation.Application.Shared.Results;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;

namespace DevStation.Tests.Integration.Api;

public class ProjectsEndpointTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _pg =
        new PostgreSqlBuilder("docker.io/library/postgres:16-alpine").Build();
    private readonly WebApplicationFactory<Program> _factory;
    private HttpClient _client = null!;

    public ProjectsEndpointTests()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
            b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = _pg.GetConnectionString(),
                ["DevStation:Token"] = "test",
                // allow temp-dir repo paths
                ["DevStation:AllowedRepoRoots:0"] = Path.GetTempPath().TrimEnd('/'),
            })));
    }

    public async Task InitializeAsync()
    {
        await _pg.StartAsync();
        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Add("Authorization", "Bearer test");
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
        await _pg.DisposeAsync();
    }

    private record ProjectDto(Guid Id, string Name, string? Technology);

    [Fact]
    public async Task Create_list_get_update_delete_roundtrip()
    {
        var created = await _client.PostAsJsonAsync("/api/projects", new
        {
            name = "demo",
            description = "d",
            localPath = Path.GetTempPath().TrimEnd('/'),
            defaultBranch = "main",
            technology = "dotnet",
            tags = new[] { "x" },
        });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var env = await created.Content.ReadFromJsonAsync<ApiEnvelope<ProjectDto>>();
        var id = env!.Data!.Id;
        Assert.Equal("demo", env.Data.Name);

        var patched = await _client.PatchAsJsonAsync($"/api/projects/{id}", new { technology = "csharp" });
        Assert.Equal(HttpStatusCode.OK, patched.StatusCode);

        var one = await _client.GetFromJsonAsync<ApiEnvelope<ProjectDto>>($"/api/projects/{id}");
        Assert.Equal("demo", one!.Data!.Name);

        // destructive op requires ?confirm=true
        var noConfirm = await _client.DeleteAsync($"/api/projects/{id}");
        Assert.Equal(HttpStatusCode.BadRequest, noConfirm.StatusCode);
        var envErr = await noConfirm.Content.ReadFromJsonAsync<ApiEnvelope<object>>();
        Assert.Equal("CONFIRMATION_REQUIRED", envErr!.Error!.Code);

        Assert.Equal(HttpStatusCode.OK,
            (await _client.DeleteAsync($"/api/projects/{id}?confirm=true")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/projects/{id}")).StatusCode);
    }

    [Fact]
    public async Task Create_invalid_returns_validation_envelope()
    {
        var res = await _client.PostAsJsonAsync("/api/projects", new { name = "" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var env = await res.Content.ReadFromJsonAsync<ApiEnvelope<object>>();
        Assert.Equal("VALIDATION_ERROR", env!.Error!.Code);
    }

    [Fact]
    public async Task Git_status_on_non_repo_returns_GIT_NOT_REPO()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ds-nonrepo-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var created = await _client.PostAsJsonAsync("/api/projects", new { name = "p", localPath = dir });
            var env = await created.Content.ReadFromJsonAsync<ApiEnvelope<ProjectDto>>();
            var res = await _client.GetAsync($"/api/projects/{env!.Data!.Id}/git");
            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
            var err = await res.Content.ReadFromJsonAsync<ApiEnvelope<object>>();
            Assert.Equal("GIT_NOT_REPO", err!.Error!.Code);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task Dashboard_summary_returns_shape()
    {
        var res = await _client.GetAsync("/api/dashboard/summary");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var env = await res.Content.ReadFromJsonAsync<ApiEnvelope<Dictionary<string, object>>>();
        Assert.NotNull(env!.Data);
        Assert.True(env.Data.ContainsKey("projects"));
        Assert.True(env.Data.ContainsKey("services"));
        Assert.True(env.Data.ContainsKey("containers"));
        Assert.True(env.Data.ContainsKey("environmentHealth"));
        Assert.True(env.Data.ContainsKey("recentActivity"));
    }

    [Fact]
    public async Task Unauthorized_without_token()
    {
        using var anon = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/projects")).StatusCode);
    }
}
