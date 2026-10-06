using DevStation.Application.Modules.Dashboard.GetDashboardSummary;
using DevStation.Application.Shared.Results;

namespace DevStation.Web.Modules.Dashboard;

public static class DashboardEndpoints
{
    public static void Map(RouteGroupBuilder api)
        => api.MapGet("/dashboard/summary",
            async (GetDashboardSummaryHandler h, CancellationToken ct)
                => ApiResult.Ok(await h.Handle(ct)));
}
