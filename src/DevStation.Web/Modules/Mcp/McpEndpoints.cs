using DevStation.Application.Modules.Mcp;
using DevStation.Application.Shared.Commands;
using DevStation.Application.Shared.Results;
using DevStation.Web.Shared.Modules;

namespace DevStation.Web.Modules.Mcp;

/// <summary>
/// Read-only MCP-style tool surface over HTTP.
/// ponytail: this is descriptors + in-process invoke only — a real stdio MCP
/// server is a separate host process wrapping these endpoints; add it when a
/// consumer actually needs the protocol (docs/api/mcp.md).
/// </summary>
public static class McpEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/mcp/tools", async (CommandRegistry registry,
                CancellationToken ct) =>
            {
                var tools = McpToolCatalog
                    .FromCommands(await registry.ForTierAsync("read", ct))
                    .Concat(McpToolCatalog.ExplicitTools)
                    .Select(t => new
                    {
                        name = t.Name,
                        description = t.Description,
                        inputSchema = McpToolCatalog.SchemaObject(t.InputSchema),
                    });
                return ApiResult.Ok(tools);
            });

        api.MapPost("/mcp/tools/{name}/invoke", async (string name,
                McpInvokeRequest r, McpToolInvoker invoker,
                CancellationToken ct)
            => ApiResult.Ok(await invoker.Invoke(name, r.Args, ct)));
    }
}
