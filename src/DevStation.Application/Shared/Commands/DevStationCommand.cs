namespace DevStation.Application.Shared.Commands;

/// <summary>
/// One palette command. <c>Verb</c>: navigation (target = route, client
/// router.push) | action (target = "{action}:{args…}" dispatched in-process
/// by ExecuteCommandHandler) | query (reserved). <c>RequiredTier</c> is a
/// lowercase tier name ranked by <see cref="Security.Tiers"/>; the execute
/// endpoint enforces it via the matching authorization policy.
/// </summary>
public sealed record DevStationCommand(
    string Id,
    string Title,
    string Verb,
    string Target,
    string RequiredTier,
    bool NeedsConfirm,
    IReadOnlyDictionary<string, string>? ParamSchema = null);
