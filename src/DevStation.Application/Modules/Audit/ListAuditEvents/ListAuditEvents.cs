using System.Text.Json.Nodes;
using DevStation.Application.Shared.Auditing;
using DevStation.Application.Shared.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DevStation.Application.Modules.Audit.ListAuditEvents;

public sealed record AuditEventDto(Guid Id, DateTimeOffset Timestamp,
    string? User, string Action, string? Resource, string? ResourceId,
    Guid? EnvironmentId, string Result, JsonNode? Metadata);

/// <summary>Newest-first audit history; metadata is redacted at read time
/// (AuditRedactor) so secret-shaped keys never leave the API.</summary>
public sealed class ListAuditEventsHandler(IDevStationDb db)
{
    public async Task<IReadOnlyList<AuditEventDto>> Handle(string? action,
        string? resource, int take, CancellationToken ct)
    {
        var rows = await db.AuditEvents.AsNoTracking()
            .Where(e => action == null || e.Action == action)
            .Where(e => resource == null || e.Resource == resource)
            .OrderByDescending(e => e.Timestamp)
            .Take(Math.Clamp(take, 1, 500))
            .ToListAsync(ct);
        return rows.Select(e => new AuditEventDto(e.Id, e.Timestamp, e.User,
            e.Action, e.Resource, e.ResourceId, e.EnvironmentId, e.Result,
            AuditRedactor.Redact(e.Metadata))).ToList();
    }
}
