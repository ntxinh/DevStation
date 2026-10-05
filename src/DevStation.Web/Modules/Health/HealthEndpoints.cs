using DevStation.Application.Shared.Results;

namespace DevStation.Web.Modules.Health;

public static class HealthModule
{
    // ponytail: placeholder listing module names — replaced by the real health module in Task 6
    public static void Map(RouteGroupBuilder api)
        => api.MapGet("/health", () => ApiResult.Ok(new { modules = new[] { "health" } }));
}
