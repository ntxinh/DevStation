using DevStation.Application.Modules.Deployments.GetDeployment;
using DevStation.Application.Shared.Ai;
using DevStation.Application.Shared.Results;

namespace DevStation.Application.Modules.Ai.ExplainError;

public sealed record ExplainErrorRequest(string? LogText, Guid? LogId);

public sealed record ExplainErrorResult(string Cause,
    List<string> Evidence, List<string> Investigation, string SuggestedFix);

/// <summary>
/// POST /api/ai/explain-error — suggestion only; never executes anything.
/// <c>LogId</c> is interpreted as a deployment id (the only log-bearing
/// entity with a durable identity) and pulls lines via GetDeploymentHandler
/// (buffer → provider fallback).
/// </summary>
public sealed class ExplainErrorHandler(IAiProvider ai,
    GetDeploymentHandler deployment)
{
    public async Task<ExplainErrorResult> Handle(ExplainErrorRequest req,
        CancellationToken ct)
    {
        if (!await ai.IsAvailableAsync(ct))
            throw new AppException("AI_UNAVAILABLE", 503,
                $"AI provider '{ai.Name}' unavailable");
        var text = req.LogText;
        if (string.IsNullOrWhiteSpace(text) && req.LogId is { } id)
            text = string.Join('\n', (await deployment.Handle(id, ct)).Logs);
        if (string.IsNullOrWhiteSpace(text))
            throw new AppException("VALIDATION_ERROR", 400,
                "logText or logId is required");

        var raw = await ai.CompleteAsync(AiPrompts.ExplainErrorSystem,
            AiPrompts.ExplainErrorUser(text), ct);
        var parsed = AiJson.Parse<Payload>(raw);
        return parsed is null
            ? new ExplainErrorResult(raw.Trim(), [], [],
                "Model returned no structured suggestion")
            : new ExplainErrorResult(parsed.Cause ?? raw.Trim(),
                parsed.Evidence ?? [], parsed.Investigation ?? [],
                parsed.SuggestedFix ?? "");
    }

    private sealed record Payload(string? Cause, List<string>? Evidence,
        List<string>? Investigation, string? SuggestedFix);
}
