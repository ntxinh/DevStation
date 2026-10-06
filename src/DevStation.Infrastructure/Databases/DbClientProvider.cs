using System.Data.Common;
using System.Diagnostics;
using DevStation.Application.Shared.Providers;
using DevStation.Domain.Entities;

namespace DevStation.Infrastructure.Databases;

/// <summary>Shared DbConnection/DbCommand mechanics; subclasses supply the
/// connection factory and dialect schema SQL.</summary>
public abstract class DbClientProvider : IDatabaseProvider
{
    public abstract string Kind { get; }

    protected abstract DbConnection Open(DatabaseConnection conn, string? password);
    protected abstract string ColumnsSql { get; }
    protected abstract string IndexesSql { get; }
    protected abstract string ViewsSql { get; }
    protected abstract string ProceduresSql { get; }
    protected virtual string ProbeSql => "SELECT 1";

    public async Task<bool> TestConnectionAsync(DatabaseConnection conn,
        string? password, CancellationToken ct)
    {
        await using var c = Open(conn, password);
        await c.OpenAsync(ct);
        await using var cmd = c.CreateCommand();
        cmd.CommandText = ProbeSql;
        cmd.CommandTimeout = 10;
        await cmd.ExecuteScalarAsync(ct);
        return true;
    }

    public async Task<QueryResult> ExecuteQueryAsync(DatabaseConnection conn,
        string sql, string? password, int maxRows, int timeoutSeconds,
        CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        await using var c = Open(conn, password);
        await c.OpenAsync(ct);
        await using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        cmd.CommandTimeout = timeoutSeconds;
        await using var r = await cmd.ExecuteReaderAsync(ct);

        var cols = (await r.GetColumnSchemaAsync(ct))
            .Select(s => new ResultColumn(s.ColumnName, s.DataTypeName ?? ""))
            .ToList();
        var rows = new List<object?[]>();
        var truncated = false;
        while (await r.ReadAsync(ct))
        {
            if (rows.Count >= maxRows) { truncated = true; break; }
            var row = new object?[cols.Count];
            r.GetValues((object[])row);
            for (var i = 0; i < row.Length; i++) row[i] = Normalize(row[i]);
            rows.Add(row);
        }
        return new QueryResult(cols, rows,
            cols.Count > 0 ? rows.Count : r.RecordsAffected,
            (int)sw.ElapsedMilliseconds, truncated);
    }

    public async Task<DatabaseSchema> GetSchemaAsync(DatabaseConnection conn,
        string? password, CancellationToken ct)
    {
        await using var c = Open(conn, password);
        await c.OpenAsync(ct);

        var indexes = new Dictionary<(string s, string t), List<IndexInfo>>();

        // tables + columns
        var colRows = await QueryAsync(c, ColumnsSql, ct);
        var tableCols = new Dictionary<(string s, string t), List<ColumnInfo>>();
        foreach (var row in colRows)
        {
            var key = ((string)row[0]!, (string)row[1]!);
            (tableCols.TryGetValue(key, out var l) ? l : tableCols[key] = [])
                .Add(new ColumnInfo((string)row[2]!, (string)row[3]!,
                    (string)row[4]! == "YES", (bool)row[5]!));
        }

        // indexes
        foreach (var row in await QueryAsync(c, IndexesSql, ct))
        {
            var key = ((string)row[0]!, (string)row[1]!);
            var idx = new IndexInfo((string)row[2]!,
                ((string)row[3]!).Split(',').Select(x => x.Trim()).ToList(),
                (bool)row[4]!);
            (indexes.TryGetValue(key, out var l) ? l : indexes[key] = []).Add(idx);
        }

        var schemaMap = new Dictionary<string, SchemaInfo>();
        foreach (var ((s, t), cols) in tableCols.OrderBy(k => k.Key.s).ThenBy(k => k.Key.t))
        {
            if (!schemaMap.TryGetValue(s, out var si))
                schemaMap[s] = si = new SchemaInfo(s, [], [], []);
            si.Tables.Add(new TableInfo(t, cols, indexes.GetValueOrDefault((s, t), [])));
        }

        foreach (var row in await QueryAsync(c, ViewsSql, ct))
        {
            var s = (string)row[0]!;
            if (!schemaMap.TryGetValue(s, out var si))
                schemaMap[s] = si = new SchemaInfo(s, [], [], []);
            si.Views.Add((string)row[1]!);
        }
        foreach (var row in await QueryAsync(c, ProceduresSql, ct))
        {
            var s = (string)row[0]!;
            if (!schemaMap.TryGetValue(s, out var si))
                schemaMap[s] = si = new SchemaInfo(s, [], [], []);
            si.Procedures.Add(new ProcedureInfo((string)row[1]!));
        }

        return new DatabaseSchema(schemaMap.Values
            .OrderBy(s => s.Name).ToList());
    }

    private static async Task<List<object?[]>> QueryAsync(DbConnection c,
        string sql, CancellationToken ct)
    {
        await using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        cmd.CommandTimeout = 30;
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var rows = new List<object?[]>();
        while (await r.ReadAsync(ct))
        {
            var row = new object?[r.FieldCount];
            r.GetValues((object[])row);
            rows.Add(row);
        }
        return rows;
    }

    /// <summary>JSON-safe scalars only: primitives pass through, bytea → base64,
    /// everything else (uuid, jsonb, inet…) → ToString.</summary>
    internal static object? Normalize(object? v) => v switch
    {
        null or DBNull => null,
        bool or byte or sbyte or short or ushort or int or uint or long or ulong
            or float or double or decimal or string => v,
        byte[] b => Convert.ToBase64String(b),
        DateTime d => d.ToString("O"),
        DateTimeOffset d => d.ToString("O"),
        DateOnly d => d.ToString("O"),
        TimeOnly t => t.ToString("O"),
        _ => v.ToString(),
    };
}
