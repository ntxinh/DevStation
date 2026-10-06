using System.Security.Claims;
using DevStation.Application.Shared.Commands;

namespace DevStation.Application.Modules.Commands.ListCommands;

/// <summary>GET /api/commands — live command set filtered by the caller's
/// tier claim. Search results are merged client-side, not here.</summary>
public sealed class ListCommandsHandler(CommandRegistry registry)
{
    public Task<IReadOnlyList<DevStationCommand>> Handle(ClaimsPrincipal user,
        CancellationToken ct)
        => registry.ForTierAsync(user.FindFirst("tier")?.Value, ct);
}
