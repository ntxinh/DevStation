using DevStation.Application.Modules.Search.SearchAll;
using DevStation.Application.Shared.Results;

namespace DevStation.Web.Modules.Search;

public static class SearchEndpoints
{
    public static void Map(RouteGroupBuilder api)
        => api.MapGet("/search", async (string? q, SearchAllHandler h,
            CancellationToken ct) => ApiResult.Ok(await h.Handle(q, ct)));
}
