using DevStation.Application.Shared.Results;

namespace DevStation.Application.Shared.Ai;

/// <summary>
/// Text-completion seam for the AI assist endpoints. Implementations only
/// produce suggestions — they can never reach the command/action layer.
/// Selected by <c>Ai:Provider</c> (ollama|openai|none, default none).
/// </summary>
public interface IAiProvider
{
    string Name { get; }
    Task<bool> IsAvailableAsync(CancellationToken ct);
    Task<string> CompleteAsync(string system, string prompt, CancellationToken ct);
}

/// <summary>Fallback when no provider is configured: reports unavailable and
/// throws AI_UNAVAILABLE 503 on completion attempts.</summary>
public sealed class NullAiProvider : IAiProvider
{
    public string Name => "none";
    public Task<bool> IsAvailableAsync(CancellationToken ct)
        => Task.FromResult(false);
    public Task<string> CompleteAsync(string system, string prompt, CancellationToken ct)
        => throw new AppException("AI_UNAVAILABLE", 503,
            "No AI provider configured (set Ai:Provider to ollama or openai)");
}

/// <summary>Bounds every AI call: 30s timeout, 8k-char input ceiling.</summary>
public static class AiLimits
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);
    public const int MaxInputChars = 8000;

    public static string Truncate(string? s)
        => s is { Length: > MaxInputChars } ? s[..MaxInputChars] : s ?? "";
}
