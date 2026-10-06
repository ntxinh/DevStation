using DevStation.Application.Shared.Providers;
using DevStation.Application.Shared.Results;
using DevStation.Infrastructure.Databases;
using DevStation.Infrastructure.Secrets;

namespace DevStation.Tests.Unit.Databases;

public class DatabaseProviderTests
{
    [Fact]
    public void Resolver_maps_kind_and_throws_for_unknown()
    {
        var providers = new IDatabaseProvider[]
        { new PostgreSqlProvider(), new SqlServerProvider() };
        Assert.Equal("postgresql", providers.For("postgresql").Kind);
        Assert.Equal("sqlserver", providers.For("sqlserver").Kind);
        var ex = Assert.Throws<AppException>(() => providers.For("mysql"));
        Assert.Equal("UNKNOWN_DB_KIND", ex.Code);
    }

    [Fact]
    public void SqlServer_schema_queries_are_shaped_for_the_contract()
    {
        // no live sqlserver in CI — the contract check is that each query
        // produces the rows GetSchemaAsync's assembler expects
        Assert.Contains("sys.tables", SqlServerProvider.ColumnsSqlText);
        Assert.Contains("sys.columns", SqlServerProvider.ColumnsSqlText);
        Assert.Contains("is_nullable", SqlServerProvider.ColumnsSqlText);
        Assert.Contains("is_primary_key", SqlServerProvider.ColumnsSqlText);
        Assert.Contains("sys.indexes", SqlServerProvider.IndexesSqlText);
        Assert.Contains("is_unique", SqlServerProvider.IndexesSqlText);
        Assert.Contains("key_ordinal", SqlServerProvider.IndexesSqlText);
        Assert.Contains("sys.views", SqlServerProvider.ViewsSqlText);
        Assert.Contains("sys.procedures", SqlServerProvider.ProceduresSqlText);
    }
}

public class EnvSecretStoreTests : IDisposable
{
    private const string EnvKey = "DEVSTATION_SECRET_DBCONN_TEST_X";

    [Fact]
    public async Task Env_var_is_read_through_normalized_key()
    {
        Environment.SetEnvironmentVariable(EnvKey, "s3cret");
        var store = new EnvSecretStore();
        Assert.Equal("s3cret", await store.GetAsync("dbconn-test-x"));
        Assert.Null(await store.GetAsync("dbconn-missing"));
    }

    [Fact]
    public async Task Runtime_set_overrides_env_and_deletes()
    {
        Environment.SetEnvironmentVariable(EnvKey, "env-val");
        var store = new EnvSecretStore();
        await store.SetAsync("dbconn-test-x", "runtime-val");
        Assert.Equal("runtime-val", await store.GetAsync("dbconn-test-x"));
        await store.DeleteAsync("dbconn-test-x");
        // runtime gone → falls back to env
        Assert.Equal("env-val", await store.GetAsync("dbconn-test-x"));
    }

    public void Dispose() => Environment.SetEnvironmentVariable(EnvKey, null);
}
