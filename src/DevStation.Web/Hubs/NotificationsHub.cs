using DevStation.Application.Shared.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace DevStation.Web.Hubs;

[Authorize("Read")]
public sealed class NotificationsHub : Hub;

/// <summary>Bridges INotificationNotifier → SignalR `notification.new` on /hubs/notifications.</summary>
public sealed class SignalRNotificationNotifier(IHubContext<NotificationsHub> hub)
    : INotificationNotifier
{
    public Task Publish(NotificationUpdate update, CancellationToken ct)
        => hub.Clients.All.SendAsync("notification.new", update, ct);
}
