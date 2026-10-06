using DevStation.Application.Modules.Databases.GetSchema;
using DevStation.Application.Shared.Ai;
using DevStation.Application.Shared.Persistence;
using DevStation.Application.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace DevStation.Application.Modules.Ai.ExplainSql;

public sealed record ExplainSqlRequest(string Sql, Guid? ConnectionId);

public sealed record ExplainSqlResult(string Explanation);

/// <summary>
/// POST /api/ai/explain-sql — plain-language explanation only. When
/// <c>ConnectionId</c> is given the live schema digest is appended to the
/// prompt. The statement is never executed.
/// </summary>
public sealed class ExplainSqlHandler(IAiProvider ai, IDevStationDb db,
    GetSchemaHandler schema)
{
    public async Task<ExplainSqlResult> Handle(ExplainSqlRequest req,
        CancellationToken ct)
    {
        if (!await ai.IsAvailableAsync(ct))
            throw new AppException("AI_UNAVAILABLE", 503,
                $"AI provider '{ai.Name}' unavailable");
        if (string.IsNullOrWhiteSpace(req.Sql))
            throw new AppException("VALIDATION_ERROR", 400, "sql is required");

        string? summary = null;
        if (req.ConnectionId is { } connId)
        {
            var conn = await db.DatabaseConnections.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == connId, ct)
                ?? throw new AppException("NOT_FOUND", 404,
                    "Connection not found");
            summary = AiPrompts.SchemaSummary(conn.Name,
                await schema.Handle(connId, ct));
        }

        var raw = await ai.CompleteAsync(AiPrompts.ExplainSqlSystem,
            AiPrompts.ExplainSqlUser(req.Sql, summary), ct);
        return new ExplainSqlResult(
            AiJson.Parse<Payload>(raw)?.Explanation ?? raw.Trim());
    }

    private sealed record Payload(string? Explanation);
}
