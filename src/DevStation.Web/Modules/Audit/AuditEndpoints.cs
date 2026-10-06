using DevStation.Application.Modules.Audit.ListAuditEvents;
using DevStation.Application.Shared.Results;

namespace DevStation.Web.Modules.Audit;

public static class AuditEndpoints
{
    public static void Map(RouteGroupBuilder api)
        => api.MapGet("/audit", async (string? action, string? resource,
            int? take, ListAuditEventsHandler h, CancellationToken ct)
            => ApiResult.Ok(await h.Handle(action, resource, take ?? 100, ct)));
}
