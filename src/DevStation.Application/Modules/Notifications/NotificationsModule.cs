using DevStation.Application.Modules.Notifications.ListNotifications;
using DevStation.Application.Modules.Notifications.MarkRead;
using Microsoft.Extensions.DependencyInjection;

namespace DevStation.Application.Modules.Notifications;

public static class NotificationsModule
{
    public static IServiceCollection AddNotificationsModule(this IServiceCollection s) => s
        .AddScoped<ListNotificationsHandler>()
        .AddScoped<MarkReadHandler>();
}
