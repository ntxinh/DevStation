using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;

namespace DevStation.Tests.Integration.Api;

/// <summary>Connect/test/disconnect lifecycle: secrets land in ISecretStore as
/// SecretRefs only — GET must expose hasCredentials, never the token.
/// No live GitHub calls; unconfigured providers return notConfigured.</summary>
public class IntegrationsEndpointTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _pg =
        new PostgreSqlBuilder("docker.io/library/postgres:16-alpine").Build();
    private readonly WebApplicationFactory<Program> _factory;
    private HttpClient _client = null!;

    public IntegrationsEndpointTests()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
            b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Default"] = _pg.GetConnectionString(),
                    ["DevStation:Token"] = "test", // main token = admin tier
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

    private static async Task<JsonElement> Data(HttpResponseMessage res)
    {
        var env = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        Assert.True(env.RootElement.GetProperty("data").ValueKind
            != JsonValueKind.Null, $"empty data: {env.RootElement}");
        return env.RootElement.GetProperty("data").Clone();
    }

    private async Task<List<JsonElement>> List()
    {
        var d = await Data(await _client.GetAsync("/api/integrations"));
        return d.EnumerateArray().Select(e => e.Clone()).ToList();
    }
    [Fact]
    public async Task List_returns_all_integration_types()
    {
        var types = (await List()).Select(i => i.GetProperty("type").GetString())
            .Order().ToList();
        Assert.Equal(["azure", "cloudflare", "github", "jira"], types);
        foreach (var i in await List())
            Assert.False(i.GetProperty("hasCredentials").GetBoolean());
    }

    [Fact]
    public async Task Connect_stores_secret_ref_and_get_shows_credentials_not_value()
    {
        var res = await _client.PostAsJsonAsync("/api/integrations/github/connect",
            new { config = new { }, secrets = new { token = "ghp_secret_test_123" } });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var dto = await Data(res);
        Assert.True(dto.GetProperty("hasCredentials").GetBoolean());
        Assert.Equal("configured", dto.GetProperty("status").GetString());
        Assert.DoesNotContain("ghp_secret_test_123", dto.GetRawText());

        var github = (await List()).Single(i => i.GetProperty("type").GetString() == "github");
        Assert.True(github.GetProperty("hasCredentials").GetBoolean());
        Assert.DoesNotContain("ghp_secret_test_123", github.GetRawText());
        Assert.DoesNotContain("credentialsRef", github.GetRawText());

        // audit row written
        var audit = await Data(await _client.GetAsync(
            "/api/audit?action=integration.connect"));
        Assert.Contains(audit.EnumerateArray(),
            e => e.GetProperty("resource").GetString() == "integration");
    }

    [Fact]
    public async Task Connect_rejects_unknown_secret_and_config_keys()
    {
        var res = await _client.PostAsJsonAsync("/api/integrations/github/connect",
            new { secrets = new { bogus = "x" } });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        res = await _client.PostAsJsonAsync("/api/integrations/jira/connect",
            new { config = new { evil = "y" } });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Unconfigured_test_returns_false_not_configured()
    {
        var res = await _client.PostAsync("/api/integrations/cloudflare/test", null);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var d = await Data(res);
        Assert.False(d.GetProperty("ok").GetBoolean());
        Assert.Equal("notconfigured", d.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Disconnect_requires_confirm_and_clears_credentials()
    {
        await _client.PostAsJsonAsync("/api/integrations/github/connect",
            new { secrets = new { token = "ghp_to_be_cleared" } });

        var noConfirm = await _client.PostAsJsonAsync(
            "/api/integrations/github/disconnect", new { confirm = false });
        Assert.Equal(HttpStatusCode.BadRequest, noConfirm.StatusCode);

        var ok = await _client.PostAsJsonAsync(
            "/api/integrations/github/disconnect", new { confirm = true });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);

        var github = (await List()).Single(i => i.GetProperty("type").GetString() == "github");
        Assert.False(github.GetProperty("hasCredentials").GetBoolean());
        Assert.Equal("notconfigured", github.GetProperty("status").GetString());
    }
}
