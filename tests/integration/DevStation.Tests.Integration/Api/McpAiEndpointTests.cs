using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DevStation.Application.Shared.Results;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;

namespace DevStation.Tests.Integration.Api;

public class McpAiEndpointTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _pg =
        new PostgreSqlBuilder("docker.io/library/postgres:16-alpine").Build();
    private readonly WebApplicationFactory<Program> _factory;
    private HttpClient _client = null!;

    public McpAiEndpointTests()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
            b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Default"] = _pg.GetConnectionString(),
                    ["DevStation:Token"] = "test",
                    ["DevStation:AllowedRepoRoots:0"] =
                        Path.GetTempPath().TrimEnd('/'),
                    // no Ai:Provider → null provider → 503 AI_UNAVAILABLE
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

    [Fact]
    public async Task Tools_lists_only_read_tools()
    {
        var res = await _client.GetFromJsonAsync<JsonElement>("/api/mcp/tools");
        var tools = res.GetProperty("data").EnumerateArray().ToList();
        var names = tools.Select(t => t.GetProperty("name").GetString())
            .ToList();

        foreach (var t in new[] { "list_projects", "get_project", "get_logs",
                     "list_environments", "run_health_check", "nav_dashboard" })
            Assert.Contains(t, names);
        Assert.DoesNotContain(names, n => n is not null &&
            (n.Contains("_restart_") || n.Contains("_trigger_")
             || n.StartsWith("deploy_")));
        Assert.All(tools, t => Assert.True(
            t.TryGetProperty("inputSchema", out _)));
    }

    [Fact]
    public async Task Invoke_list_projects_returns_rows()
    {
        // seed one project so the row list is non-empty
        var created = await _client.PostAsJsonAsync("/api/projects",
            new { name = "mcp-proj" });
        created.EnsureSuccessStatusCode();

        var res = await _client.PostAsJsonAsync("/api/mcp/tools/list_projects/invoke",
            new { args = new { } });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        var rows = body.GetProperty("data").EnumerateArray().ToList();
        Assert.NotEmpty(rows);
        Assert.Contains(rows, r =>
            r.GetProperty("name").GetString() == "mcp-proj");
    }

    [Fact]
    public async Task Invoke_unknown_tool_404()
    {
        var res = await _client.PostAsJsonAsync(
            "/api/mcp/tools/definitely_not_a_tool/invoke", new { args = new { } });
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("MCP_TOOL_UNKNOWN",
            body.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Invoke_action_command_name_404()
    {
        // an action-tier command must never be invocable through MCP even if
        // someone guesses its normalized name
        var res = await _client.PostAsJsonAsync(
            "/api/mcp/tools/service_restart_x/invoke", new { args = new { } });
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Ai_endpoints_503_when_provider_none()
    {
        foreach (var (path, payload) in new (string, object)[]
        {
            ("/api/ai/explain-error", new { logText = "boom NPE at line 3" }),
            ("/api/ai/explain-sql", new { sql = "select 1" }),
            ("/api/ai/generate-sql", new { question = "list tables",
                connectionId = Guid.NewGuid() }),
        })
        {
            var res = await _client.PostAsJsonAsync(path, payload);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, res.StatusCode);
            var body = await res.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("AI_UNAVAILABLE",
                body.GetProperty("error").GetProperty("code").GetString());
        }
    }
}
