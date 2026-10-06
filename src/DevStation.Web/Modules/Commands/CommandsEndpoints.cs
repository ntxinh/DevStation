using System.Globalization;
using System.Security.Claims;
using DevStation.Application.Modules.Commands.ExecuteCommand;
using DevStation.Application.Modules.Commands.ListCommands;
using DevStation.Application.Shared.Commands;
using DevStation.Application.Shared.Results;
using DevStation.Web.Shared.Modules;
using Microsoft.AspNetCore.Authorization;

namespace DevStation.Web.Modules.Commands;

public static class CommandsEndpoints
{
    private static string Policy(string tier)
        => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(tier);

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/commands", async (ListCommandsHandler h,
                ClaimsPrincipal u, CancellationToken ct)
            => ApiResult.Ok(await h.Handle(u, ct)));

        // tier isn't static — it's per-command, so resolve then run the
        // matching policy via AuthorizeAsync (same shape as POST /deployments).
        api.MapPost("/commands/{id}/execute", async (string id,
                ExecuteCommandRequest r, CommandRegistry registry,
                ExecuteCommandHandler h, IAuthorizationService auth,
                ClaimsPrincipal u, CancellationToken ct) =>
            {
                var cmd = await registry.FindAsync(id, ct)
                    ?? throw new AppException("NOT_FOUND", 404,
                        "Command not found");
                if (!(await auth.AuthorizeAsync(u, null,
                        Policy(cmd.RequiredTier))).Succeeded)
                    return Results.Forbid();
                if (cmd.NeedsConfirm) ConfirmGate.Require(r.Confirm);
                return ApiResult.Ok(await h.Handle(cmd, r, u, ct));
            });
    }
}
