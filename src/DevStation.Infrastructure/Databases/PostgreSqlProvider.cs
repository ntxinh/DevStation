using System.Data.Common;
using DevStation.Domain.Entities;
using Npgsql;

namespace DevStation.Infrastructure.Databases;

public sealed class PostgreSqlProvider : DbClientProvider
{
    public override string Kind => "postgresql";

    protected override DbConnection Open(DatabaseConnection conn, string? password)
        => new NpgsqlConnection(new NpgsqlConnectionStringBuilder
        {
            Host = conn.Host,
            Port = conn.Port > 0 ? conn.Port : 5432,
            Database = conn.Database,
            Username = conn.Username,
            Password = password ?? "",
            Timeout = 10,
        }.ConnectionString);

    // row: schema, table, column, type, nullable('YES'|'NO'), is_pk(bool)
    protected override string ColumnsSql => """
        SELECT c.table_schema, c.table_name, c.column_name, c.data_type,
               c.is_nullable,
               EXISTS (
                 SELECT 1
                 FROM information_schema.table_constraints tc
                 JOIN information_schema.key_column_usage kcu
                   ON tc.constraint_name = kcu.constraint_name
                  AND tc.table_schema = kcu.table_schema
                  AND tc.table_name = kcu.table_name
                 WHERE tc.constraint_type = 'PRIMARY KEY'
                   AND tc.table_schema = c.table_schema
                   AND tc.table_name = c.table_name
                   AND kcu.column_name = c.column_name) AS is_pk
        FROM information_schema.columns c
        JOIN information_schema.tables t
          ON t.table_schema = c.table_schema AND t.table_name = c.table_name
        WHERE t.table_type = 'BASE TABLE'
          AND c.table_schema NOT IN ('pg_catalog', 'information_schema')
        ORDER BY c.table_schema, c.table_name, c.ordinal_position
        """;

    // row: schema, table, index_name, columns_csv, is_unique — includes PK indexes
    protected override string IndexesSql => """
        SELECT n.nspname AS schema_name, t.relname AS table_name,
               i.relname AS index_name,
               string_agg(a.attname, ',' ORDER BY k.ord) AS columns_csv,
               ix.indisunique
        FROM pg_index ix
        JOIN pg_class i ON i.oid = ix.indexrelid
        JOIN pg_class t ON t.oid = ix.indrelid
        JOIN pg_namespace n ON n.oid = t.relnamespace
        JOIN unnest(ix.indkey) WITH ORDINALITY AS k(attnum, ord) ON true
        JOIN pg_attribute a ON a.attrelid = t.oid AND a.attnum = k.attnum
        WHERE n.nspname NOT IN ('pg_catalog', 'information_schema')
        GROUP BY n.nspname, t.relname, i.relname, ix.indisunique
        ORDER BY n.nspname, t.relname, i.relname
        """;

    // row: schema, view_name
    protected override string ViewsSql => """
        SELECT table_schema, table_name
        FROM information_schema.views
        WHERE table_schema NOT IN ('pg_catalog', 'information_schema')
        ORDER BY table_schema, table_name
        """;

    // row: schema, routine_name (functions + procedures)
    protected override string ProceduresSql => """
        SELECT routine_schema, routine_name
        FROM information_schema.routines
        WHERE routine_schema NOT IN ('pg_catalog', 'information_schema')
        ORDER BY routine_schema, routine_name
        """;
}
