using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DevStation.Application.Shared.Logging;
using DevStation.Domain.Entities;
using DevStation.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace DevStation.Tests.Integration.Api;

public class LogsAuditSearchEndpointTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _pg =
        new PostgreSqlBuilder("docker.io/library/postgres:16-alpine").Build();
    private readonly WebApplicationFactory<Program> _factory;
    private HttpClient _client = null!;

    public LogsAuditSearchEndpointTests()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
            b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(
                new Dictionary<string, string?>
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

    private DevStationDbContext NewDb()
    {
        var scope = _factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<DevStationDbContext>();
    }

    private static readonly JsonSerializerOptions Json =
        new() { PropertyNameCaseInsensitive = true };

    private record LogLine(DateTimeOffset Ts, string Level, string Source,
        Guid? EnvironmentId, string Message);
    private record LogEnvelope(List<LogLine> Data);
    private record AuditRow(Guid Id, DateTimeOffset Timestamp, string? User,
        string Action, string? Resource, string Result, JsonElement? Metadata);
    private record AuditEnvelope(List<AuditRow> Data);
    private record SearchGroup(List<SearchHit> Projects, List<SearchHit> Environments,
        List<SearchHit> Services, List<SearchHit> Jobs, List<SearchHit> Databases,
        List<SearchHit> Deployments, List<SearchHit> Integrations,
        List<SearchHit> Containers);
    private record SearchHit(string Id, string Name, string? Detail, string? Status);
    private record SearchEnvelope(SearchGroup Data);

    [Fact]
    public async Task Sink_write_appears_in_get_logs()
    {
        _factory.Services.GetRequiredService<ILogSink>().Write(
            new LogEntry(DateTimeOffset.UtcNow, "info", "api", null,
                "marker-unique-log-line"));

        var body = await _client.GetFromJsonAsync<LogEnvelope>(
            "/api/logs?q=marker-unique-log-line", Json);
        Assert.Contains(body!.Data, l => l.Message == "marker-unique-log-line"
            && l.Source == "api" && l.Level == "info");
    }

    [Fact]
    public async Task Get_logs_filters_by_source()
    {
        _factory.Services.GetRequiredService<ILogSink>().Write(
            new LogEntry(DateTimeOffset.UtcNow, "warn", "srcfilter",
                null, "source-filter-line"));

        var body = await _client.GetFromJsonAsync<LogEnvelope>(
            "/api/logs?source=srcfilter", Json);
        Assert.Contains(body!.Data, l => l.Message == "source-filter-line");
        Assert.All(body.Data, l => Assert.Equal("srcfilter", l.Source));
    }

    [Fact]
    public async Task Export_downloads_plain_text_attachment()
    {
        _factory.Services.GetRequiredService<ILogSink>().Write(
            new LogEntry(DateTimeOffset.UtcNow, "info", "api", null,
                "export-marker-line"));

        var res = await _client.GetAsync("/api/logs/export?q=export-marker-line");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("text/plain", res.Content.Headers.ContentType?.MediaType);
        Assert.Contains("logs.txt",
            res.Content.Headers.ContentDisposition?.ToString() ?? "");
        var text = await res.Content.ReadAsStringAsync();
        Assert.Contains("export-marker-line", text);
    }

    [Fact]
    public async Task Audit_list_returns_events_with_secret_metadata_redacted()
    {
        await using var db = NewDb();
        db.AuditEvents.Add(new AuditEvent
        {
            Action = "project.create",
            Resource = "project",
            User = "tester",
            Result = "ok",
            Metadata = """{"name":"demo","dbPassword":"supersecret"}""",
        });
        await db.SaveChangesAsync();

        var body = await _client.GetFromJsonAsync<AuditEnvelope>(
            "/api/audit?action=project.create", Json);
        var row = Assert.Single(body!.Data,
            e => e.Action == "project.create" && e.User == "tester");
        var meta = row.Metadata!.Value;
        Assert.Equal("***", meta.GetProperty("dbPassword").GetString());
        Assert.Equal("demo", meta.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Search_returns_grouped_matches()
    {
        await using var db = NewDb();
        db.Projects.Add(new Project { Name = "searchable-proj", Description = "alpha" });
        db.JobDefinitions.Add(new JobDefinition
        {
            Name = "searchable-job",
            Type = "noop",
        });
        await db.SaveChangesAsync();

        var body = await _client.GetFromJsonAsync<SearchEnvelope>(
            "/api/search?q=searchable", Json);
        Assert.Contains(body!.Data.Projects, p => p.Name == "searchable-proj");
        Assert.Contains(body.Data.Jobs, j => j.Name == "searchable-job");
    }

    [Fact]
    public async Task Search_with_short_query_returns_empty_groups()
    {
        var body = await _client.GetFromJsonAsync<SearchEnvelope>("/api/search?q=x", Json);
        Assert.Empty(body!.Data.Projects);
        Assert.Empty(body.Data.Jobs);
        Assert.Empty(body.Data.Containers);
    }

    [Fact]
    public async Task Endpoints_require_auth()
    {
        var anon = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anon.GetAsync("/api/logs")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anon.GetAsync("/api/audit")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anon.GetAsync("/api/search?q=ab")).StatusCode);
    }
}
