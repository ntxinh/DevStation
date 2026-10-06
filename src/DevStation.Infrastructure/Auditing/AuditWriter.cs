using System.Text.Json;
using DevStation.Application.Shared.Auditing;
using DevStation.Domain.Entities;
using DevStation.Infrastructure.Persistence;

namespace DevStation.Infrastructure.Auditing;

public sealed class AuditWriter(DevStationDbContext db) : IAuditWriter
{
    public async Task Record(string action, string resource, Guid? resourceId,
        string? user, string result, object? metadata, CancellationToken ct = default)
    {
        db.AuditEvents.Add(new AuditEvent
        {
            Action = action,
            Resource = resource,
            ResourceId = resourceId?.ToString(),
            User = user,
            Result = result,
            Metadata = metadata is null ? null : JsonSerializer.Serialize(metadata),
        });
        await db.SaveChangesAsync(ct);
    }
}
