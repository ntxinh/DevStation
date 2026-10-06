using System.Security.Claims;
using DevStation.Application.Shared.Auditing;
using DevStation.Application.Shared.Persistence;
using DevStation.Application.Shared.Results;
using DevStation.Application.Shared.Secrets;
using DevStation.Domain.Entities;
using DevStation.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace DevStation.Application.Modules.Databases.CreateConnection;

public sealed record CreateConnectionRequest
{
    public string Name { get; init; } = "";
    public string Kind { get; init; } = "postgresql";
    public Guid? EnvironmentId { get; init; }
    public string Host { get; init; } = "";
    public int Port { get; init; }
    public string Database { get; init; } = "";
    public string Username { get; init; } = "";
    public string? Password { get; init; }
    public bool ReadOnly { get; init; } = true;
    public int MaxRows { get; init; } = 1000;
    public int TimeoutSeconds { get; init; } = 30;
}

public sealed class CreateConnectionHandler(IDevStationDb db, ISecretStore secrets,
    IAuditWriter audit)
{
    public async Task<ConnectionDto> Handle(CreateConnectionRequest r,
        ClaimsPrincipal user, CancellationToken ct)
    {
        var errors = new List<string>();
        ConnectionValidator.Check(errors, r.Name, r.Kind, r.Host, r.Port,
            r.Database, r.Username, r.MaxRows, r.TimeoutSeconds);
        if (errors.Count > 0)
            throw new AppException("VALIDATION_ERROR", 400, string.Join("; ", errors));
        if (r.EnvironmentId is { } envId &&
            !await db.Environments.AnyAsync(e => e.Id == envId, ct))
            throw new AppException("VALIDATION_ERROR", 400, "environmentId not found");

        var c = new DatabaseConnection
        {
            Name = r.Name.Trim(),
            Kind = r.Kind,
            EnvironmentId = r.EnvironmentId,
            Host = r.Host.Trim(),
            Port = r.Port,
            Database = r.Database.Trim(),
            Username = r.Username.Trim(),
            ReadOnly = r.ReadOnly,
            MaxRows = r.MaxRows,
            TimeoutSeconds = r.TimeoutSeconds,
        };
        db.DatabaseConnections.Add(c);
        await db.SaveChangesAsync(ct);

        if (!string.IsNullOrEmpty(r.Password))
        {
            var key = $"dbconn-{c.Id}";
            await secrets.SetAsync(key, r.Password, ct);
            c.PasswordRef = new SecretRef(key);
            await db.SaveChangesAsync(ct);
        }

        var envType = r.EnvironmentId is { } eid
            ? await db.Environments.Where(e => e.Id == eid)
                .Select(e => e.Type).FirstOrDefaultAsync(ct)
            : null;
        await audit.Record("dbconnection.create", "database_connection", c.Id,
            user.Identity?.Name, "ok", new { c.Name, c.Kind, c.ReadOnly }, ct);
        return ConnectionDto.Of(c, envType);
    }
}
