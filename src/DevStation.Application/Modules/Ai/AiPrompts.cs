using DevStation.Application.Shared.Ai;

namespace DevStation.Application.Modules.Ai;

/// <summary>Pure prompt text for the three AI endpoints — kept static so unit
/// tests can assert the shape without a provider.</summary>
public static class AiPrompts
{
    public const string ExplainErrorSystem =
        "You are a senior SRE debugging assistant for the DevStation ops tool. " +
        "Analyze the error log the user provides and answer ONLY with a JSON " +
        "object: {\"cause\": string, \"evidence\": string[], " +
        "\"investigation\": string[], \"suggestedFix\": string}. " +
        "No prose outside the JSON.";

    public const string ExplainSqlSystem =
        "You are a database assistant for the DevStation ops tool. Explain " +
        "what the given SQL statement does — tables touched, filters, " +
        "performance concerns — in plain language. Answer ONLY with JSON: " +
        "{\"explanation\": string}.";

    public const string GenerateSqlSystem =
        "You are a database assistant for the DevStation ops tool. Translate " +
        "the user's question into ONE read-only SELECT statement for their " +
        "database. Never emit INSERT/UPDATE/DELETE/DDL. Answer ONLY with " +
        "JSON: {\"sql\": string, \"explanation\": string}. " +
        "If you cannot answer safely, return {\"sql\": null, \"explanation\": why}.";

    public static string ExplainErrorUser(string logText)
        => $"Log excerpt:\n{AiLimits.Truncate(logText)}";

    public static string ExplainSqlUser(string sql, string? schemaSummary)
        => schemaSummary is null
            ? $"SQL:\n{AiLimits.Truncate(sql)}"
            : $"Schema:\n{AiLimits.Truncate(schemaSummary)}\n\nSQL:\n{AiLimits.Truncate(sql)}";

    public static string GenerateSqlUser(string question, string schemaSummary)
        => $"Schema:\n{AiLimits.Truncate(schemaSummary)}\n\nQuestion: {AiLimits.Truncate(question)}";

    /// <summary>Compact schema digest for prompts — one line per table with
    /// column names/types; pk flags kept as '*'. Truncated by the callers to
    /// the AI input ceiling.</summary>
    public static string SchemaSummary(string connectionName,
        Shared.Providers.DatabaseSchema schema)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("connection: ").Append(connectionName).Append('\n');
        foreach (var s in schema.Schemas)
        {
            foreach (var t in s.Tables)
                sb.Append(s.Name).Append('.').Append(t.Name)
                  .Append('(').Append(string.Join(", ",
                      t.Columns.Select(c =>
                          $"{c.Name}:{c.Type}{(c.IsPk ? "*" : "")}")))
                  .Append(")\n");
            foreach (var v in s.Views)
                sb.Append(s.Name).Append('.').Append(v).Append(" (view)\n");
        }
        return sb.ToString();
    }
}
