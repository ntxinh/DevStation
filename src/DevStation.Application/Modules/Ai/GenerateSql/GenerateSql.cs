using DevStation.Application.Modules.Databases.GetSchema;
using DevStation.Application.Shared.Ai;
using DevStation.Application.Shared.Persistence;
using DevStation.Application.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace DevStation.Application.Modules.Ai.GenerateSql;

public sealed record GenerateSqlRequest(string Question, Guid ConnectionId);

public sealed record GenerateSqlResult(string? Sql, string Explanation);

/// <summary>
/// POST /api/ai/generate-sql — returns a candidate SELECT plus explanation.
/// The SQL is advisory: execution still goes through
/// POST /api/databases/connections/{id}/execute with its own SqlGuard
/// tiers/confirm gate — this handler never runs the statement.
/// </summary>
public sealed class GenerateSqlHandler(IAiProvider ai, IDevStationDb db,
    GetSchemaHandler schema)
{
    public async Task<GenerateSqlResult> Handle(GenerateSqlRequest req,
        CancellationToken ct)
    {
        if (!await ai.IsAvailableAsync(ct))
            throw new AppException("AI_UNAVAILABLE", 503,
                $"AI provider '{ai.Name}' unavailable");
        if (string.IsNullOrWhiteSpace(req.Question))
            throw new AppException("VALIDATION_ERROR", 400,
                "question is required");
        var conn = await db.DatabaseConnections.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == req.ConnectionId, ct)
            ?? throw new AppException("NOT_FOUND", 404, "Connection not found");

        var raw = await ai.CompleteAsync(AiPrompts.GenerateSqlSystem,
            AiPrompts.GenerateSqlUser(req.Question,
                AiPrompts.SchemaSummary(conn.Name,
                    await schema.Handle(req.ConnectionId, ct))), ct);

        var parsed = AiJson.Parse<Payload>(raw);
        return parsed is not null
            ? new GenerateSqlResult(parsed.Sql, parsed.Explanation ?? "")
            // model ignored the JSON contract — treat output as raw SQL
            : new GenerateSqlResult(AiJson.ExtractJsonBlock(raw) is null
                    ? raw.Trim() : raw, "");
    }

    private sealed record Payload(string? Sql, string? Explanation);
}
