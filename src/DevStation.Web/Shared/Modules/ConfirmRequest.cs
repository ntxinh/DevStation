using DevStation.Application.Shared.Results;

namespace DevStation.Web.Shared.Modules;

/// <summary>Body-bound confirmation shape for destructive POST/PATCH endpoints;
/// DELETE endpoints take <c>?confirm=true</c> (bodies on DELETE are awkward).</summary>
public sealed record ConfirmRequest(bool Confirm);

public static class ConfirmGate
{
    public static void Require(bool confirm)
    {
        if (!confirm)
            throw new AppException("CONFIRMATION_REQUIRED", 400,
                "Destructive action requires confirmation (confirm=true)");
    }
}
