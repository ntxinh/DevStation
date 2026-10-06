using System.Security.Claims;
using System.Text.Json;
using DevStation.Application.Modules.Containers.RestartContainer;
using DevStation.Application.Modules.Deployments.StartDeployment;
using DevStation.Application.Modules.Jobs.TriggerJob;
using DevStation.Application.Modules.Services.RestartService;
using DevStation.Application.Shared.Auditing;
using DevStation.Application.Shared.Commands;
using DevStation.Application.Shared.Results;

namespace DevStation.Application.Modules.Commands.ExecuteCommand;

public sealed record ExecuteCommandRequest(JsonElement? Params, bool Confirm);

/// <summary>Outcome of a dispatch: navigation → Route for router.push;
/// action → Result payload from the underlying handler.</summary>
public sealed record CommandResult(string? Route, object? Result);

/// <summary>
/// Dispatches an action command in-process to the same handlers the module
/// endpoints use (restart service/container, trigger job, start deployment) —
/// never an internal HTTP call. Records a command.execute audit row on success;
/// failures propagate as AppException like the direct endpoints'.
/// </summary>
public sealed class ExecuteCommandHandler(
    RestartServiceHandler restartService,
    RestartContainerHandler restartContainer,
    TriggerJobHandler triggerJob,
    StartDeploymentHandler startDeployment,
    IAuditWriter audit)
{
    /// <summary>Action command targets encode "action:arg[:arg…]".</summary>
    public static string[] SplitTarget(string target)
        => target.Split(':', StringSplitOptions.RemoveEmptyEntries);

    public async Task<CommandResult> Handle(DevStationCommand cmd,
        ExecuteCommandRequest req, ClaimsPrincipal user, CancellationToken ct)
    {
        if (cmd.Verb == "navigation")
            return new CommandResult(cmd.Target, null);
        if (cmd.Verb != "action")
            throw new AppException("COMMAND_NOT_EXECUTABLE", 422,
                $"command verb '{cmd.Verb}' cannot be executed");

        var parts = SplitTarget(cmd.Target);
        object? result = parts switch
        {
            ["service.restart", { } id] => await restartService.Handle(
                ParseGuid(id, "service"), user, ct),
            ["container.restart", { } id] => await ContainerRestart(id, user, ct),
            ["job.trigger", { } id] => await triggerJob.Handle(
                ParseGuid(id, "job"), user, ct),
            ["deploy", { } pid, { } eid] => await startDeployment.Handle(
                ParseGuid(eid, "environment"),
                new StartDeploymentRequest(ParseGuid(pid, "project"),
                    CommitParam(req.Params)),
                user, ct),
            _ => throw new AppException("COMMAND_UNKNOWN", 400,
                $"unrecognized command target '{cmd.Target}'"),
        };
        await audit.Record("command.execute", "command", null,
            user.Identity?.Name, "ok", new { cmd.Id, cmd.Title }, ct);
        return new CommandResult(null, result);
    }

    // ContainerActionHandler returns Task (no payload) — wrap to fit the switch.
    private async Task<object?> ContainerRestart(string id, ClaimsPrincipal user,
        CancellationToken ct)
    {
        await restartContainer.Handle(id, user, ct);
        return null;
    }

    private static Guid ParseGuid(string raw, string what)
        => Guid.TryParse(raw, out var g) ? g
            : throw new AppException("COMMAND_TARGET_INVALID", 400,
                $"command target carries a malformed {what} id");

    private static string? CommitParam(JsonElement? p)
        => p is { ValueKind: JsonValueKind.Object } el
            && el.TryGetProperty("commit", out var c)
            && c.ValueKind == JsonValueKind.String
            ? c.GetString() : null;
}
