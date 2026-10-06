using System.Text.Json.Nodes;
using DevStation.Application.Shared.Auditing;
using DevStation.Application.Shared.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DevStation.Application.Modules.Notifications.ListNotifications;

public sealed record NotificationDto(Guid Id, string Type, string Title,
    string? Message, string? Link, bool Read, string? Severity,
    JsonNode? Metadata, DateTimeOffset CreatedAt);

/// <summary>Newest-first list; unread=true filters to unread rows.
/// Metadata was redacted at write time, but read-side redaction runs too —
/// same rule as audit rows.</summary>
public sealed class ListNotificationsHandler(IDevStationDb db)
{
    public async Task<IReadOnlyList<NotificationDto>> Handle(bool unread,
        int take, CancellationToken ct)
        => (await db.Notifications.AsNoTracking()
            .Where(n => !unread || !n.Read)
            .OrderByDescending(n => n.CreatedAt)
            .Take(Math.Clamp(take, 1, 500))
            .ToListAsync(ct))
            .Select(n => new NotificationDto(n.Id, n.Type, n.Title, n.Message,
                n.Link, n.Read, n.Severity, AuditRedactor.Redact(n.Metadata),
                n.CreatedAt))
            .ToList();
}
