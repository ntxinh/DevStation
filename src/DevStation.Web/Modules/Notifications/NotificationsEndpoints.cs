using DevStation.Application.Modules.Notifications.ListNotifications;
using DevStation.Application.Modules.Notifications.MarkRead;
using DevStation.Application.Shared.Results;

namespace DevStation.Web.Modules.Notifications;

public static class NotificationsEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/notifications", async (bool? unread, int? take,
                ListNotificationsHandler h, CancellationToken ct)
            => ApiResult.Ok(await h.Handle(unread == true, take ?? 50, ct)));
        api.MapPost("/notifications/{id:guid}/read", async (Guid id,
                MarkReadHandler h, CancellationToken ct) =>
            {
                await h.Handle(id, ct);
                return ApiResult.Ok(new { read = true });
            });
        api.MapPost("/notifications/read-all", async (MarkReadHandler h,
                CancellationToken ct)
            => ApiResult.Ok(new { updated = await h.HandleAll(ct) }));
    }
}
