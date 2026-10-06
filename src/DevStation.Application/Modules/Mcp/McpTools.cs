using System.Text.Json;
using DevStation.Application.Shared.Commands;
using DevStation.Application.Shared.Security;

namespace DevStation.Application.Modules.Mcp;

/// <summary>MCP-style tool descriptor served by GET /api/mcp/tools.</summary>
public sealed record McpTool(string Name, string Description,
    IReadOnlyDictionary<string, string> InputSchema);

/// <summary>
/// Builds the read-only tool surface: every CommandRegistry command with a
/// navigation/query verb at read tier (ids normalized ':'/'.' → '_') plus the
/// explicit data tools McpToolInvoker dispatches. Action-tier commands are
/// never listed — this surface is read-only.
/// </summary>
public static class McpToolCatalog
{
    public static string ToolNameFor(string commandId)
        => commandId.Replace(':', '_').Replace('.', '_');

    public static bool IsMcpVisible(DevStationCommand c)
        => c.Verb is "navigation" or "query"
           && Tiers.Rank(c.RequiredTier) <= Tiers.Rank("read")
           && !c.NeedsConfirm;

    public static IReadOnlyList<McpTool> FromCommands(
        IEnumerable<DevStationCommand> commands)
        => commands.Where(IsMcpVisible).Select(c => new McpTool(
            ToolNameFor(c.Id), c.Title,
            c.ParamSchema ?? EmptySchema)).ToList();

    private static readonly IReadOnlyDictionary<string, string> EmptySchema =
        new Dictionary<string, string>();

    /// <summary>Explicit read tools — one per stable read query. Invocation
    /// routes in-process to the same scoped handlers the REST endpoints use.</summary>
    public static readonly IReadOnlyList<McpTool> ExplicitTools =
    [
        new("list_projects", "List projects (optionally filtered by q)",
            new Dictionary<string, string> { ["q"] = "string?" }),
        new("get_project", "Get one project by id",
            new Dictionary<string, string> { ["id"] = "guid" }),
        new("get_git_status", "Git status of a project's local repo",
            new Dictionary<string, string> { ["id"] = "guid" }),
        new("list_environments", "List environments", EmptySchema),
        new("get_environment_health", "Environment detail incl. health checks",
            new Dictionary<string, string> { ["id"] = "guid" }),
        new("list_services", "List services (optionally by environmentId)",
            new Dictionary<string, string> { ["environmentId"] = "guid?" }),
        new("list_containers", "List containers from the local runtime",
            EmptySchema),
        new("list_jobs", "List job definitions with last run",
            EmptySchema),
        new("list_deployments", "Deployment history (environmentId?, projectId?, take?)",
            new Dictionary<string, string>
            {
                ["environmentId"] = "guid?",
                ["projectId"] = "guid?",
                ["take"] = "int?",
            }),
        new("get_logs", "Buffered app logs (source?, severity?, q?, take?)",
            new Dictionary<string, string>
            {
                ["source"] = "string?",
                ["severity"] = "string?",
                ["q"] = "string?",
                ["take"] = "int?",
            }),
        new("run_health_check", "Run one health check now and return the result",
            new Dictionary<string, string> { ["id"] = "guid" }),
    ];


    /// <summary>JSON-Schema-ish object form for MCP clients.</summary>
    public static object SchemaObject(IReadOnlyDictionary<string, string> schema)
        => new
        {
            type = "object",
            properties = schema.ToDictionary(
                kv => kv.Key,
                kv => (object)new { type = kv.Value.TrimEnd('?') switch
                {
                    "guid" => "string",
                    "int" => "integer",
                    _ => kv.Value.TrimEnd('?'),
                } }),
            required = schema.Where(kv => !kv.Value.EndsWith('?'))
                .Select(kv => kv.Key).ToArray(),
        };
}

/// <summary>POST body for /api/mcp/tools/{name}/invoke.</summary>
public sealed record McpInvokeRequest(JsonElement? Args);
