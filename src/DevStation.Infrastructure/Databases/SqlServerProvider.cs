using System.Data.Common;
using DevStation.Domain.Entities;
using Microsoft.Data.SqlClient;

namespace DevStation.Infrastructure.Databases;

/// <summary>SQL Server provider — same contract as PostgreSqlProvider.
/// Unit tests cover the schema SQL text shape; live runs need a real server.</summary>
public sealed class SqlServerProvider : DbClientProvider
{
    public override string Kind => "sqlserver";

    protected override DbConnection Open(DatabaseConnection conn, string? password)
        => new SqlConnection(new SqlConnectionStringBuilder
        {
            DataSource = conn.Port > 0 ? $"{conn.Host},{conn.Port}" : conn.Host,
            InitialCatalog = conn.Database,
            UserID = conn.Username,
            Password = password ?? "",
            ConnectTimeout = 10,
            // dev-station connections are typically to local/test servers
            TrustServerCertificate = true,
        }.ConnectionString);

    // row: schema, table, column, type, nullable('YES'|'NO'), is_pk(bool)
    public const string ColumnsSqlText = """
        SELECT s.name, t.name, c.name, ty.name,
               CASE WHEN c.is_nullable = 1 THEN 'YES' ELSE 'NO' END,
               CAST(CASE WHEN pk.column_id IS NULL THEN 0 ELSE 1 END AS bit)
        FROM sys.tables t
        JOIN sys.schemas s ON s.schema_id = t.schema_id
        JOIN sys.columns c ON c.object_id = t.object_id
        JOIN sys.types ty ON ty.user_type_id = c.user_type_id
        LEFT JOIN (
            SELECT ic.object_id, ic.column_id
            FROM sys.indexes i
            JOIN sys.index_columns ic
              ON ic.object_id = i.object_id AND ic.index_id = i.index_id
            WHERE i.is_primary_key = 1
        ) pk ON pk.object_id = t.object_id AND pk.column_id = c.column_id
        ORDER BY s.name, t.name, c.column_id
        """;

    // row: schema, table, index_name, columns_csv, is_unique
    public const string IndexesSqlText = """
        SELECT s.name, t.name, i.name,
               STRING_AGG(c.name, ',') WITHIN GROUP (ORDER BY ic.key_ordinal),
               CAST(i.is_unique AS bit)
        FROM sys.indexes i
        JOIN sys.tables t ON t.object_id = i.object_id
        JOIN sys.schemas s ON s.schema_id = t.schema_id
        JOIN sys.index_columns ic
          ON ic.object_id = i.object_id AND ic.index_id = i.index_id
        JOIN sys.columns c
          ON c.object_id = ic.object_id AND c.column_id = ic.column_id
        WHERE i.type > 0 AND i.is_primary_key = 0 AND ic.is_included_column = 0
        GROUP BY s.name, t.name, i.name, i.is_unique
        ORDER BY s.name, t.name, i.name
        """;

    // row: schema, view_name
    public const string ViewsSqlText = """
        SELECT s.name, v.name
        FROM sys.views v
        JOIN sys.schemas s ON s.schema_id = v.schema_id
        ORDER BY s.name, v.name
        """;

    // row: schema, procedure_name
    public const string ProceduresSqlText = """
        SELECT s.name, p.name
        FROM sys.procedures p
        JOIN sys.schemas s ON s.schema_id = p.schema_id
        ORDER BY s.name, p.name
        """;

    protected override string ColumnsSql => ColumnsSqlText;
    protected override string IndexesSql => IndexesSqlText;
    protected override string ViewsSql => ViewsSqlText;
    protected override string ProceduresSql => ProceduresSqlText;
}
