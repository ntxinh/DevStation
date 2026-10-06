using DevStation.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace DevStation.Tests.Integration.Persistence;

public class MigrationSmokeTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _pg =
        new PostgreSqlBuilder("docker.io/library/postgres:16-alpine").Build();

    public Task InitializeAsync() => _pg.StartAsync();
    public Task DisposeAsync() => _pg.DisposeAsync().AsTask();

    [Fact]
    public async Task Migrations_create_all_tables()
    {
        var opt = new DbContextOptionsBuilder<DevStationDbContext>()
            .UseNpgsql(_pg.GetConnectionString()).Options;
        await using var db = new DevStationDbContext(opt);
        await db.Database.MigrateAsync();
        var tables = await db.Database
            .SqlQuery<string>($"select tablename from pg_tables where schemaname='public'")
            .ToListAsync();
        foreach (var t in new[]
        {
            "projects", "environments", "services", "database_connections",
            "job_definitions", "job_executions", "deployments", "health_checks",
            "health_check_results", "integrations", "audit_events",
            "notifications", "saved_queries", "query_history"
        })
            Assert.Contains(t, tables);
    }
}
