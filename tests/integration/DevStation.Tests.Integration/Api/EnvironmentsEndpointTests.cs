using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DevStation.Application.Shared.Results;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;

namespace DevStation.Tests.Integration.Api;

public class EnvironmentsEndpointTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _pg =
        new PostgreSqlBuilder("docker.io/library/postgres:16-alpine").Build();
    private readonly WebApplicationFactory<Program> _factory;
    private HttpClient _client = null!;

    public EnvironmentsEndpointTests()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
            b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = _pg.GetConnectionString(),
                ["DevStation:Token"] = "test",
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

    private record VarDto(string Key, string? Value, bool Secret);
    private record EnvDto(Guid Id, string Name, string Type, string Health,
        List<VarDto> Variables);
    private record CompareVar(string Key, bool Differs, bool Masked);
    private record CompareService(string Name, bool InA, bool InB);
    private record Pair<T>(T A, T B);
    private record CompareDto(Pair<string?> Version, Pair<string?> Commit,
        Pair<DateTimeOffset?> DeployedAt, List<CompareService> Services,
        List<CompareVar> Variables);

    [Fact]
    public async Task Environment_crud_roundtrip_masks_secrets()
    {
        var created = await _client.PostAsJsonAsync("/api/environments", new
        {
            name = "staging", type = "staging", baseUrl = "https://stg.example.com",
            variables = new[]
            {
                new { key = "LOG_LEVEL", value = "debug", secret = false },
                new { key = "API_KEY", value = "super-secret-123", secret = true },
            },
        });
        var raw = await created.Content.ReadAsStringAsync();
        Assert.True(created.StatusCode == HttpStatusCode.OK, raw);
        Assert.DoesNotContain("super-secret-123", raw);
        var id = JsonSerializer.Deserialize<JsonElement>(raw)
            .GetProperty("data").GetProperty("id").GetGuid();

        var got = await _client.GetFromJsonAsync<ApiEnvelope<JsonElement>>(
            $"/api/environments/{id}");
        var envJson = got!.Data.GetProperty("environment");
        Assert.DoesNotContain("super-secret-123", envJson.GetRawText());

        var patched = await _client.PatchAsJsonAsync($"/api/environments/{id}",
            new { type = "production" });
        Assert.Equal(HttpStatusCode.OK, patched.StatusCode);

        var bad = await _client.PostAsJsonAsync("/api/environments",
            new { name = "", type = "qa" });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);

        Assert.Equal(HttpStatusCode.BadRequest,
            (await _client.DeleteAsync($"/api/environments/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await _client.DeleteAsync($"/api/environments/{id}?confirm=true")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await _client.GetAsync($"/api/environments/{id}")).StatusCode);
    }

    [Fact]
    public async Task Compare_masks_secret_variables_and_flags_diffs()
    {
        var a = await CreateEnv("env-a", ("SHARED", "1", false),
            ("DIFFERS", "x", false), ("SECRET", "s1", true));
        var b = await CreateEnv("env-b", ("SHARED", "1", false),
            ("DIFFERS", "y", false), ("ONLY_B", "z", false));

        var res = await _client.GetAsync($"/api/environments/compare?a={a}&b={b}");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadAsStringAsync();
        Assert.DoesNotContain("s1", body);
        var env = JsonSerializer.Deserialize<ApiEnvelope<CompareDto>>(body,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var c = env!.Data!;

        Assert.False(c.Variables.Single(v => v.Key == "SHARED").Differs);
        var differs = c.Variables.Single(v => v.Key == "DIFFERS");
        Assert.True(differs.Differs);
        Assert.False(differs.Masked);
        var secret = c.Variables.Single(v => v.Key == "SECRET");
        Assert.True(secret.Masked);
        Assert.True(secret.Differs); // present in a, absent in b
        Assert.True(c.Variables.Single(v => v.Key == "ONLY_B").Differs);
    }

    private async Task<Guid> CreateEnv(string name,
        params (string key, string value, bool secret)[] vars)
    {
        var res = await _client.PostAsJsonAsync("/api/environments", new
        {
            name,
            type = "qa",
            variables = vars.Select(v => new { key = v.key, value = v.value, secret = v.secret }),
        });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var env = await res.Content.ReadFromJsonAsync<ApiEnvelope<EnvDto>>();
        return env!.Data!.Id;
    }

    private record HealthCheckDto(Guid Id, string Name, string Kind, string Target,
        bool Enabled);
    private record ResultDto(string Status, int? LatencyMs, string? Error,
        DateTimeOffset CheckedAt);
    private record CheckItem(HealthCheckDto Check, ResultDto? Last, double Uptime24h);
    private record OverviewDto(List<CheckItem> Checks);

    [Fact]
    public async Task Health_check_create_run_overview_against_testcontainer_pg()
    {
        // target the testcontainer's pg port — a real tcp check against a live service
        var (host, port) = ParsePgEndpoint();
        var created = await _client.PostAsJsonAsync("/api/health", new
        {
            name = "pg tcp", kind = "tcp",
            target = $"{host}:{port}", intervalSeconds = 60,
        });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var check = (await created.Content
            .ReadFromJsonAsync<ApiEnvelope<HealthCheckDto>>())!.Data!;

        var ran = await _client.PostAsync($"/api/health/{check.Id}/check", null);
        Assert.Equal(HttpStatusCode.OK, ran.StatusCode);
        var result = (await ran.Content
            .ReadFromJsonAsync<ApiEnvelope<ResultDto>>())!.Data!;
        Assert.Equal("healthy", result.Status);

        var overview = await _client.GetFromJsonAsync<ApiEnvelope<OverviewDto>>(
            "/api/health/overview");
        var item = overview!.Data!.Checks.Single(x => x.Check.Id == check.Id);
        Assert.NotNull(item.Last);
        Assert.Equal("healthy", item.Last!.Status);
        Assert.Equal(100, item.Uptime24h);
    }

    private (string host, int port) ParsePgEndpoint()
    {
        var cs = _pg.GetConnectionString()
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Split('='))
            .ToDictionary(p => p[0].Trim(), p => p[1].Trim(), StringComparer.OrdinalIgnoreCase);
        return (cs["Host"], int.Parse(cs["Port"]));
    }
}
