namespace DevStation.Application.Shared.Notifications;

public sealed record NotificationUpdate(Guid Id, string Kind, string Title,
    string? Body, string? Severity, DateTimeOffset CreatedAt);

/// <summary>Publishes `notification.new` to /hubs/notifications; implemented in Web (SignalR).</summary>
public interface INotificationNotifier
{
    Task Publish(NotificationUpdate update, CancellationToken ct);
}

/// <summary>
/// Persists a Notification row and broadcasts it. Metadata is serialized and
/// run through the audit-key redactor before storage — notifications may carry
/// job/deployment context but never secrets.
/// </summary>
public interface INotificationService
{
    Task NotifyAsync(string kind, string title, string? body, string? severity,
        object? metadata = null, CancellationToken ct = default);
}
