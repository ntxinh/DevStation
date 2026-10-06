using System.Text.Json;
using DevStation.Application.Modules.Containers.ListContainers;
using DevStation.Application.Modules.Deployments.ListDeployments;
using DevStation.Application.Modules.Environments.GetEnvironment;
using DevStation.Application.Modules.Environments.ListEnvironments;
using DevStation.Application.Modules.Health.RunHealthCheck;
using DevStation.Application.Modules.Jobs.ListJobs;
using DevStation.Application.Modules.Logs.GetLogs;
using DevStation.Application.Modules.Projects.GetGitStatus;
using DevStation.Application.Modules.Projects.GetProject;
using DevStation.Application.Modules.Projects.ListProjects;
using DevStation.Application.Modules.Services.ListServices;
using DevStation.Application.Shared.Commands;
using DevStation.Application.Shared.Logging;
using DevStation.Application.Shared.Results;
using Microsoft.Extensions.DependencyInjection;

namespace DevStation.Application.Modules.Mcp;

/// <summary>
/// Routes a tool name + args to the same scoped read handlers the REST
/// endpoints call — in-process, no HTTP hop. Navigation commands resolve to
/// their route. Unknown names → MCP_TOOL_UNKNOWN 404.
/// </summary>
public sealed class McpToolInvoker(IServiceProvider sp, CommandRegistry commands)
{
    public async Task<object?> Invoke(string name, JsonElement? args,
        CancellationToken ct)
    {
        // explicit read tools first (stable names independent of entity ids)
        if (McpToolCatalog.ExplicitTools.Any(t => t.Name == name))
            return await Dispatch(name, args, ct);

        // registry-mapped read commands (nav.*, *.open:<id>, future query verbs)
        var cmds = await commands.AllAsync(ct);
        var cmd = cmds.FirstOrDefault(c =>
            McpToolCatalog.IsMcpVisible(c)
            && McpToolCatalog.ToolNameFor(c.Id) == name);
        if (cmd is not null)
            return cmd.Verb == "navigation"
                ? new { route = cmd.Target }
                : throw new AppException("MCP_TOOL_UNKNOWN", 404,
                    $"query command '{cmd.Id}' has no dispatch");

        throw new AppException("MCP_TOOL_UNKNOWN", 404,
            $"unknown tool '{name}'");
    }

    private async Task<object?> Dispatch(string name, JsonElement? args,
        CancellationToken ct)
    {
        string? Str(string key)
            => Arg(args, key) is { ValueKind: JsonValueKind.String } e
                ? e.GetString() : null;
        int? Int(string key)
            => Arg(args, key) is { ValueKind: JsonValueKind.Number } e
            && e.TryGetInt32(out var n) ? n : null;
        Guid Guid_(string key)
            => Guid.TryParse(Str(key), out var g) ? g
                : throw new AppException("VALIDATION_ERROR", 400,
                    $"argument '{key}' must be a guid");
        Guid? OptGuid(string key)
            => Str(key) is { } s ? Guid_(key) : null;

        return name switch
        {
            "list_projects" => await sp.GetRequiredService<ListProjectsHandler>()
                .Handle(Str("q"), ct),
            "get_project" => await sp.GetRequiredService<GetProjectHandler>()
                .Handle(Guid_("id"), ct),
            "get_git_status" => await sp.GetRequiredService<GetGitStatusHandler>()
                .Handle(Guid_("id"), ct),
            "list_environments" => await sp
                .GetRequiredService<ListEnvironmentsHandler>().Handle(ct),
            "get_environment_health" => await sp
                .GetRequiredService<GetEnvironmentHandler>().Handle(Guid_("id"), ct),
            "list_services" => await sp.GetRequiredService<ListServicesHandler>()
                .Handle(OptGuid("environmentId"), ct),
            "list_containers" => await sp
                .GetRequiredService<ListContainersHandler>().Handle(ct),
            "list_jobs" => await sp.GetRequiredService<ListJobsHandler>()
                .Handle(ct),
            "list_deployments" => await sp
                .GetRequiredService<ListDeploymentsHandler>()
                .Handle(OptGuid("environmentId"), OptGuid("projectId"),
                    Int("take"), ct),
            "get_logs" => await sp.GetRequiredService<GetLogsHandler>()
                .Handle(new LogFilter(Str("source"), Str("severity"), null,
                    Str("q"), Int("take") ?? 100), ct),
            "run_health_check" => await sp
                .GetRequiredService<RunHealthCheckHandler>()
                .Handle(Guid_("id"), ct),
            _ => throw new AppException("MCP_TOOL_UNKNOWN", 404,
                $"unknown tool '{name}'"),
        };
    }

    private static JsonElement? Arg(JsonElement? args, string key)
        => args is { ValueKind: JsonValueKind.Object } o
            && o.TryGetProperty(key, out var v) ? v : null;
}
