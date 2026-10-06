using System.Security.Claims;
using DevStation.Application.Shared.Auditing;
using DevStation.Application.Shared.Persistence;
using DevStation.Application.Shared.Results;
using DevStation.Application.Shared.Secrets;
using Microsoft.EntityFrameworkCore;

namespace DevStation.Application.Modules.Databases.UpdateConnection;

public sealed record UpdateConnectionRequest
{
    public string? Name { get; init; }
    public string? Kind { get; init; }
    public Guid? EnvironmentId { get; init; }
    public bool UnlinkEnvironment { get; init; }
    public string? Host { get; init; }
    public int? Port { get; init; }
    public string? Database { get; init; }
    public string? Username { get; init; }
    public string? Password { get; init; }    // set only when non-empty
    public bool ClearPassword { get; init; }
    public bool? ReadOnly { get; init; }
    public int? MaxRows { get; init; }
    public int? TimeoutSeconds { get; init; }
}

public sealed class UpdateConnectionHandler(IDevStationDb db, ISecretStore secrets,
    IAuditWriter audit)
{
    public async Task<ConnectionDto> Handle(Guid id, UpdateConnectionRequest r,
        ClaimsPrincipal user, CancellationToken ct)
    {
        var c = await db.DatabaseConnections.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new AppException("NOT_FOUND", 404, "Connection not found");

        var errors = new List<string>();
        ConnectionValidator.Check(errors,
            r.Name ?? "ok", r.Kind, r.Host ?? "ok", r.Port ?? 1,
            r.Database ?? "ok", r.Username ?? "ok",
            r.MaxRows ?? c.MaxRows, r.TimeoutSeconds ?? c.TimeoutSeconds);
        if (errors.Count > 0)
            throw new AppException("VALIDATION_ERROR", 400, string.Join("; ", errors));

        if (r.Name is not null) c.Name = r.Name.Trim();
        if (r.Kind is not null) c.Kind = r.Kind;
        if (r.UnlinkEnvironment) c.EnvironmentId = null;
        else if (r.EnvironmentId is { } envId)
        {
            if (!await db.Environments.AnyAsync(e => e.Id == envId, ct))
                throw new AppException("VALIDATION_ERROR", 400, "environmentId not found");
            c.EnvironmentId = envId;
        }
        if (r.Host is not null) c.Host = r.Host.Trim();
        if (r.Port is { } p) c.Port = p;
        if (r.Database is not null) c.Database = r.Database.Trim();
        if (r.Username is not null) c.Username = r.Username.Trim();
        if (r.ReadOnly is { } ro) c.ReadOnly = ro;
        if (r.MaxRows is { } m) c.MaxRows = m;
        if (r.TimeoutSeconds is { } t) c.TimeoutSeconds = t;
        c.UpdatedAt = DateTimeOffset.UtcNow;

        if (r.ClearPassword && c.PasswordRef is { } old)
        {
            await secrets.DeleteAsync(old.Key, ct);
            c.PasswordRef = null;
        }
        else if (!string.IsNullOrEmpty(r.Password))
        {
            var key = c.PasswordRef?.Key ?? $"dbconn-{c.Id}";
            await secrets.SetAsync(key, r.Password, ct);
            c.PasswordRef = new(key);
        }

        await db.SaveChangesAsync(ct);
        var envType = c.EnvironmentId is { } eid
            ? await db.Environments.Where(e => e.Id == eid)
                .Select(e => e.Type).FirstOrDefaultAsync(ct)
            : null;
        await audit.Record("dbconnection.update", "database_connection", c.Id,
            user.Identity?.Name, "ok", new { c.Name, c.ReadOnly }, ct);
        return ConnectionDto.Of(c, envType);
    }
}
