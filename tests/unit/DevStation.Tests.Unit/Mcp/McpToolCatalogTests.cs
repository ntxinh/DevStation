using DevStation.Application.Modules.Mcp;
using DevStation.Application.Shared.Commands;
using Xunit;

namespace DevStation.Tests.Unit.Mcp;

public class McpToolCatalogTests
{
    private static DevStationCommand Cmd(string id, string verb,
        string tier = "read", bool confirm = false)
        => new(id, "title-" + id, verb, "/x", tier, confirm);

    [Fact]
    public void Command_mapping_keeps_only_read_tier_navigation_and_query()
    {
        var tools = McpToolCatalog.FromCommands([
            Cmd("nav.dashboard", "navigation"),
            Cmd("project.open:1", "navigation"),
            Cmd("search.x", "query"),
            Cmd("service.restart:1", "action", "execute"),
            Cmd("deploy:a:b", "action", "deploy", confirm: true),
            Cmd("admin.purge", "query", "admin"),
        ]);

        var names = tools.Select(t => t.Name).ToList();
        Assert.Equal(["nav_dashboard", "project_open_1", "search_x"], names);
    }

    [Fact]
    public void Explicit_tools_are_all_read_queries()
    {
        var expected = new[]
        {
            "list_projects", "get_project", "get_git_status",
            "list_environments", "get_environment_health", "list_services",
            "list_containers", "list_jobs", "list_deployments", "get_logs",
            "run_health_check",
        };
        Assert.Equal(expected.OrderBy(x => x),
            McpToolCatalog.ExplicitTools.Select(t => t.Name).OrderBy(x => x));
        // no write-ish tool ever leaks into the surface
        Assert.DoesNotContain(McpToolCatalog.ExplicitTools.Select(t => t.Name),
            n => n.Contains("restart") || n.Contains("trigger") ||
                 n.Contains("cancel") || n.Contains("delete") ||
                 n.Contains("stop"));
    }

    [Fact]
    public void Schema_object_marks_required_args()
    {
        var tool = McpToolCatalog.ExplicitTools.First(t => t.Name == "get_project");
        var schema = McpToolCatalog.SchemaObject(tool.InputSchema);
        var json = System.Text.Json.JsonSerializer.Serialize(schema);
        Assert.Contains("\"required\":[\"id\"]", json);
    }
}
