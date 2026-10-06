using DevStation.Domain.Entities;

namespace DevStation.Application.Shared.Providers;

public sealed record ColumnInfo(string Name, string Type, bool Nullable, bool IsPk);
public sealed record IndexInfo(string Name, List<string> Columns, bool Unique);
public sealed record TableInfo(string Name, List<ColumnInfo> Columns, List<IndexInfo> Indexes);
public sealed record ProcedureInfo(string Name);
public sealed record SchemaInfo(string Name, List<TableInfo> Tables,
    List<string> Views, List<ProcedureInfo> Procedures);
public sealed record DatabaseSchema(List<SchemaInfo> Schemas);

public sealed record ResultColumn(string Name, string DataType);
public sealed record QueryResult(List<ResultColumn> Columns, List<object?[]> Rows,
    int RowCount, int DurationMs, bool Truncated);

/// <summary>Kind-agnostic database provider surface. Resolved per-connection via
/// <c>conn.Kind</c>. Implementations never see the secret store — the resolved
/// password is passed in by the caller.</summary>
public interface IDatabaseProvider
{
    /// <summary>Lowercase kind key matching <see cref="DatabaseConnection.Kind"/>.</summary>
    string Kind { get; }

    /// <summary>Open a connection and run a trivial probe (SELECT 1).</summary>
    Task<bool> TestConnectionAsync(DatabaseConnection conn, string? password,
        CancellationToken ct);

    Task<DatabaseSchema> GetSchemaAsync(DatabaseConnection conn, string? password,
        CancellationToken ct);

    /// <summary>Run user SQL. Row reading stops after <paramref name="maxRows"/>
    /// (Truncated=true when hit). <paramref name="timeoutSeconds"/> → CommandTimeout;
    /// ct cancels the command mid-flight.</summary>
    Task<QueryResult> ExecuteQueryAsync(DatabaseConnection conn, string sql,
        string? password, int maxRows, int timeoutSeconds, CancellationToken ct);
}

public static class DatabaseProviders
{
    /// <summary>Maps <c>conn.Kind</c> → registered provider; throws UNKNOWN_DB_KIND.</summary>
    public static IDatabaseProvider For(this IEnumerable<IDatabaseProvider> ps,
        string kind)
        => ps.FirstOrDefault(p => p.Kind == kind)
           ?? throw new Results.AppException("UNKNOWN_DB_KIND", 400,
               $"no provider registered for kind '{kind}'");
}
