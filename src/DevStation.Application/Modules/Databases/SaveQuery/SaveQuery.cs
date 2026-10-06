using System.Security.Claims;
using DevStation.Application.Shared.Auditing;
using DevStation.Application.Shared.Persistence;
using DevStation.Application.Shared.Results;
using DevStation.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DevStation.Application.Modules.Databases.SaveQuery;

public sealed record SavedQueryDto(Guid Id, Guid? ConnectionId, string Name,
    string Sql, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt)
{
    public static SavedQueryDto Of(SavedQuery q) => new(q.Id, q.ConnectionId,
        q.Name, q.Sql, q.CreatedAt, q.UpdatedAt);
}

public sealed record SaveQueryRequest
{
    public Guid? ConnectionId { get; init; }
    public string Name { get; init; } = "";
    public string Sql { get; init; } = "";
}

public sealed class SaveQueryHandler(IDevStationDb db, IAuditWriter audit)
{
    public async Task<SavedQueryDto> Handle(SaveQueryRequest r,
        ClaimsPrincipal user, CancellationToken ct)
    {
        var errors = new List<string>();
        if (r.Name.Trim() is "" or { Length: > 200 })
            errors.Add("name must be 1-200 characters");
        if (string.IsNullOrWhiteSpace(r.Sql) || r.Sql.Length > 1_000_000)
            errors.Add("sql must be 1-1000000 characters");
        if (r.ConnectionId is { } cid &&
            !await db.DatabaseConnections.AnyAsync(c => c.Id == cid, ct))
            errors.Add("connectionId not found");
        if (errors.Count > 0)
            throw new AppException("VALIDATION_ERROR", 400, string.Join("; ", errors));

        var q = new SavedQuery
        {
            ConnectionId = r.ConnectionId,
            Name = r.Name.Trim(),
            Sql = r.Sql,
        };
        db.SavedQueries.Add(q);
        await db.SaveChangesAsync(ct);
        await audit.Record("savedquery.create", "saved_query", q.Id,
            user.Identity?.Name, "ok", new { q.Name }, ct);
        return SavedQueryDto.Of(q);
    }
}
