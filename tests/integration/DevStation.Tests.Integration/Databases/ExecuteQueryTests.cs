using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DevStation.Application.Shared.Results;
using DevStation.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;

namespace DevStation.Tests.Integration.Databases;

/// <summary>Real pg testcontainer: guardrails (readonly, prod-env, confirm),
/// row cap, and the QueryHistory audit row.</summary>
public class ExecuteQueryTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _pg =
        new PostgreSqlBuilder("docker.io/library/postgres:16-alpine").Build();
    private readonly WebApplicationFactory<Program> _factory;
    private HttpClient _client = null!;

    public ExecuteQueryTests()
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

        // seed a table + view + index in the app db so schema assertions have meat
        var b = new NpgsqlConnectionStringBuilder(_pg.GetConnectionString());
        await using var c = new NpgsqlConnection(b.ConnectionString);
        await c.OpenAsync();
        await using var cmd = c.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS ds_it_t (id serial PRIMARY KEY, name text);
            CREATE OR REPLACE VIEW ds_it_v AS SELECT id FROM ds_it_t;
            CREATE INDEX IF NOT EXISTS ds_it_i ON ds_it_t (name);
            CREATE OR REPLACE FUNCTION ds_it_f() RETURNS int LANGUAGE sql AS 'SELECT 1';
            """;
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
        await _pg.DisposeAsync();
    }

    private async Task<Guid> CreateConn(string name, bool readOnly,
        Guid? envId = null)
    {
        var b = new NpgsqlConnectionStringBuilder(_pg.GetConnectionString());
        var res = await _client.PostAsJsonAsync("/api/databases/connections", new
        {
            name, kind = "postgresql", environmentId = envId,
            host = b.Host, port = b.Port, database = b.Database,
            username = b.Username, password = b.Password,
            readOnly,
        });
        var body = await res.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        // password must never come back — no raw value field, only the flag.
        // (container pw is literally "postgres" so we check the property name)
        Assert.DoesNotContain("\"password\":", body);
        var dto = JsonSerializer.Deserialize<ApiEnvelope<ConnDto>>(body,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.True(dto!.Data!.HasPassword);
        return dto.Data.Id;
    }

    private async Task<(HttpStatusCode status, JsonElement env)> Exec(
        Guid connId, string sql, bool confirm = false, int? maxRows = null)
    {
        var url = $"/api/databases/connections/{connId}/execute"
                  + (confirm ? "?confirm=true" : "");
        var res = await _client.PostAsJsonAsync(url,
            new { sql, maxRows });
        var env = JsonSerializer.Deserialize<JsonElement>(
            await res.Content.ReadAsStringAsync());
        return (res.StatusCode, env);
    }

    private static string ErrCode(JsonElement env)
        => env.GetProperty("error").GetProperty("code").GetString()!;

    private record ConnDto(Guid Id, string Name, string Kind,
        bool HasPassword, bool ReadOnly);

    [Fact]
    public async Task Select_returns_rows_columns_and_duration()
    {
        var id = await CreateConn("it-select", readOnly: true);
        var (st, env) = await Exec(id, "SELECT 1 AS n, 'x' AS s");
        Assert.Equal(HttpStatusCode.OK, st);
        var result = env.GetProperty("data").GetProperty("result");
        Assert.Equal(1, result.GetProperty("rowCount").GetInt32());
        Assert.True(result.GetProperty("durationMs").GetInt32() >= 0);
        var cols = result.GetProperty("columns");
        Assert.Equal("n", cols[0].GetProperty("name").GetString());
        Assert.Equal(1, result.GetProperty("rows")[0][0].GetInt32());
        Assert.False(result.GetProperty("truncated").GetBoolean());
    }

    [Fact]
    public async Task Readonly_conn_blocks_writes_and_dangerous()
    {
        var id = await CreateConn("it-ro", readOnly: true);
        Assert.Equal("SQL_READONLY",
            ErrCode((await Exec(id, "INSERT INTO ds_it_t (name) VALUES ('x')")).env));
        var (st, env) = await Exec(id, "INSERT INTO ds_it_t (name) VALUES ('x')");
        Assert.Equal(HttpStatusCode.Forbidden, st);
        Assert.Equal("SQL_READONLY",
            ErrCode((await Exec(id, "DROP TABLE ds_it_t", confirm: true)).env));
    }


    [Fact]
    public async Task Select_into_is_write_and_blocked_on_readonly()
    {
        var id = await CreateConn("it-into-ro", readOnly: true);
        var (st, env) = await Exec(id, "SELECT * INTO ds_it_into FROM ds_it_t");
        Assert.Equal(HttpStatusCode.Forbidden, st);
        Assert.Equal("SQL_READONLY", ErrCode(env));
    }

    [Fact]
    public async Task Rejected_query_writes_audit_row()
    {
        var id = await CreateConn("it-audit-ro", readOnly: true);
        var (st, _) = await Exec(id,
            "INSERT INTO ds_it_t (name) VALUES ('rejected')");
        Assert.Equal(HttpStatusCode.Forbidden, st);

        await using var db = NewDb();
        var ev = await db.AuditEvents
            .Where(a => a.Action == "db.query.rejected"
                        && a.ResourceId == id.ToString())
            .SingleAsync();
        Assert.Equal("blocked:SQL_READONLY", ev.Result);
        Assert.Contains("INSERT INTO ds_it_t", ev.Metadata);
        // rejected attempts don't reach the query history
        Assert.False(await db.QueryHistory
            .AnyAsync(h => h.ConnectionId == id && h.Sql.Contains("rejected")));
    }

    [Fact]
    public async Task Health_check_database_kind_probes_connection()
    {
        var id = await CreateConn("it-health", readOnly: true);
        var created = await _client.PostAsJsonAsync("/api/health", new
        {
            name = "db probe", kind = "database", target = "it-health",
            intervalSeconds = 60, timeoutSeconds = 10,
        });
        var checkId = JsonSerializer.Deserialize<JsonElement>(
                await created.Content.ReadAsStringAsync())
            .GetProperty("data").GetProperty("id").GetGuid();

        var run = await _client.PostAsync($"/api/health/{checkId}/check", null);
        var env = JsonSerializer.Deserialize<JsonElement>(
            await run.Content.ReadAsStringAsync());
        Assert.Equal("healthy",
            env.GetProperty("data").GetProperty("status").GetString());

        // non-existent target name → unhealthy, not a 500
        var bad = await _client.PostAsJsonAsync("/api/health", new
        {
            name = "db probe missing", kind = "database",
            target = "no-such-conn", intervalSeconds = 60,
        });
        var badId = JsonSerializer.Deserialize<JsonElement>(
                await bad.Content.ReadAsStringAsync())
            .GetProperty("data").GetProperty("id").GetGuid();
        var run2 = await _client.PostAsync($"/api/health/{badId}/check", null);
        var env2 = JsonSerializer.Deserialize<JsonElement>(
            await run2.Content.ReadAsStringAsync());
        Assert.Equal("unhealthy",
            env2.GetProperty("data").GetProperty("status").GetString());
    }
    [Fact]
    public async Task Dangerous_needs_confirm_then_runs()
    {
        var id = await CreateConn("it-dev-rw", readOnly: false);
        var blocked = await Exec(id, "CREATE TABLE ds_it_new (a int)");
        Assert.Equal(HttpStatusCode.Forbidden, blocked.status);
        Assert.Equal("CONFIRMATION_REQUIRED", ErrCode(blocked.env));

        var ok = await Exec(id, "CREATE TABLE ds_it_new (a int)", confirm: true);
        Assert.Equal(HttpStatusCode.OK, ok.status);
    }

    [Fact]
    public async Task Prod_env_conn_blocks_writes_even_with_confirm()
    {
        var env = await _client.PostAsJsonAsync("/api/environments",
            new { name = "prod-env", type = "production" });
        var envId = JsonSerializer.Deserialize<JsonElement>(
                await env.Content.ReadAsStringAsync())
            .GetProperty("data").GetProperty("id").GetGuid();

        var id = await CreateConn("it-prod", readOnly: false, envId: envId);
        var noConfirm = await Exec(id, "INSERT INTO ds_it_t (name) VALUES ('x')");
        Assert.Equal("SQL_PROD_WRITE_BLOCKED", ErrCode(noConfirm.env));
        var withConfirm = await Exec(id, "DROP TABLE ds_it_t", confirm: true);
        Assert.Equal("SQL_PROD_WRITE_BLOCKED", ErrCode(withConfirm.env));
        // reads still fine on prod
        var read = await Exec(id, "SELECT 1");
        Assert.Equal(HttpStatusCode.OK, read.status);
    }

    [Fact]
    public async Task Maxrows_caps_and_flags_truncated()
    {
        var id = await CreateConn("it-cap", readOnly: true);
        var (st, env) = await Exec(id,
            "SELECT generate_series(1, 50) AS n", maxRows: 10);
        Assert.Equal(HttpStatusCode.OK, st);
        var result = env.GetProperty("data").GetProperty("result");
        Assert.Equal(10, result.GetProperty("rowCount").GetInt32());
        Assert.True(result.GetProperty("truncated").GetBoolean());
        Assert.Equal(10, result.GetProperty("rows").GetArrayLength());
    }

    [Fact]
    public async Task Failed_query_is_reported_and_logged()
    {
        var id = await CreateConn("it-fail", readOnly: true);
        var (st, env) = await Exec(id, "SELECT * FROM nonexistent_table_xyz");
        Assert.Equal(HttpStatusCode.BadRequest, st);
        Assert.Equal("QUERY_FAILED", ErrCode(env));

        await using var db = NewDb();
        var row = await db.QueryHistory
            .Where(h => h.ConnectionId == id && h.Sql.Contains("nonexistent"))
            .SingleAsync();
        Assert.False(row.Success);
        Assert.NotNull(row.Error);
        Assert.Contains("QUERY_FAILED", row.Error);
    }

    [Fact]
    public async Task Every_execution_writes_history_and_lists()
    {
        var id = await CreateConn("it-hist", readOnly: true);
        await Exec(id, "SELECT 1");
        await Exec(id, "SELECT 2");

        var list = await _client.GetFromJsonAsync<ApiEnvelope<JsonElement>>(
            $"/api/databases/history?connectionId={id}");
        Assert.Equal(2, list!.Data.GetArrayLength());
        Assert.Equal("SELECT 2",
            list.Data[0].GetProperty("sql").GetString()); // newest first
    }

    [Fact]
    public async Task Schema_returns_tables_views_indexes_procs()
    {
        var id = await CreateConn("it-schema", readOnly: true);
        var res = await _client.GetAsync(
            $"/api/databases/connections/{id}/schema");
        var env = JsonSerializer.Deserialize<JsonElement>(
            await res.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var schemas = env.GetProperty("data").GetProperty("schemas");
        var pub = schemas.EnumerateArray()
            .First(s => s.GetProperty("name").GetString() == "public");
        var table = pub.GetProperty("tables").EnumerateArray()
            .First(t => t.GetProperty("name").GetString() == "ds_it_t");
        var idCol = table.GetProperty("columns").EnumerateArray()
            .First(c => c.GetProperty("name").GetString() == "id");
        Assert.True(idCol.GetProperty("isPk").GetBoolean());
        Assert.Equal("integer", idCol.GetProperty("type").GetString());
        Assert.Contains(table.GetProperty("indexes").EnumerateArray(),
            i => i.GetProperty("name").GetString() == "ds_it_i"
                 && i.GetProperty("columns")[0].GetString() == "name");
        Assert.Contains(pub.GetProperty("views").EnumerateArray(),
            v => v.GetString() == "ds_it_v");
        Assert.Contains(pub.GetProperty("procedures").EnumerateArray(),
            p => p.GetProperty("name").GetString() == "ds_it_f");
    }

    [Fact]
    public async Task Saved_queries_roundtrip()
    {
        var id = await CreateConn("it-saved", readOnly: true);
        var saved = await _client.PostAsJsonAsync("/api/databases/saved-queries",
            new { connectionId = id, name = "q1", sql = "SELECT 1" });
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var list = await _client.GetFromJsonAsync<ApiEnvelope<JsonElement>>(
            $"/api/databases/saved-queries?connectionId={id}");
        var qid = list!.Data[0].GetProperty("id").GetGuid();
        var del = await _client.DeleteAsync($"/api/databases/saved-queries/{qid}");
        Assert.Equal(HttpStatusCode.OK, del.StatusCode);
    }

    [Fact]
    public async Task Test_endpoint_reports_reachability()
    {
        var id = await CreateConn("it-test", readOnly: true);
        var res = await _client.PostAsync(
            $"/api/databases/connections/{id}/test", null);
        var env = JsonSerializer.Deserialize<JsonElement>(
            await res.Content.ReadAsStringAsync());
        Assert.True(env.GetProperty("data").GetProperty("ok").GetBoolean());
    }

    private DevStationDbContext NewDb()
        => _factory.Services.CreateScope().ServiceProvider
            .GetRequiredService<DevStationDbContext>();
}
