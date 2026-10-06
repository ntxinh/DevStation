using System.Text.Json;
using DevStation.Application.Shared.Auditing;
using DevStation.Application.Shared.Notifications;
using DevStation.Application.Shared.Persistence;
using DevStation.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace DevStation.Infrastructure.Notifications;

public sealed class NotificationService(IDevStationDb db,
    INotificationNotifier? notifier = null,
    ILogger<NotificationService>? logger = null)
    : INotificationService
{
    /// <summary>Serialize + redact metadata for the notification row.</summary>
    public static string? RedactMetadata(object? metadata)
        => metadata is null ? null
            : AuditRedactor.Redact(JsonSerializer.Serialize(metadata))?.ToJsonString();

    public async Task NotifyAsync(string kind, string title, string? body,
        string? severity, object? metadata = null, CancellationToken ct = default)
    {
        var n = new Notification
        {
            Type = kind,
            Title = title,
            Message = body,
            Severity = severity,
            Metadata = RedactMetadata(metadata),
        };
        db.Notifications.Add(n);
        await db.SaveChangesAsync(ct);

        if (notifier is null) return;
        try
        {
            await notifier.Publish(new NotificationUpdate(n.Id, n.Type, n.Title,
                n.Message, n.Severity, n.CreatedAt), ct);
        }
        catch (Exception ex) // hub unreachable must not fail the write
        {
            logger?.LogWarning(ex, "notification.new publish failed");
        }
    }
}
