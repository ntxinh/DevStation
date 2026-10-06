using System.Security.Claims;
using DevStation.Application.Shared.Auditing;
using DevStation.Application.Shared.Persistence;
using DevStation.Application.Shared.Providers;
using DevStation.Application.Shared.Results;
using DevStation.Application.Shared.Secrets;
using Microsoft.EntityFrameworkCore;

namespace DevStation.Application.Modules.Databases.TestConnection;

public sealed record TestConnectionResult(bool Ok, string? Error);

public sealed class TestConnectionHandler(IDevStationDb db,
    IEnumerable<IDatabaseProvider> providers, ISecretStore secrets,
    IAuditWriter audit)
{
    public async Task<TestConnectionResult> Handle(Guid id, ClaimsPrincipal user,
        CancellationToken ct)
    {
        var c = await db.DatabaseConnections.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new AppException("NOT_FOUND", 404, "Connection not found");
        var pw = c.PasswordRef is null ? null
            : await secrets.GetAsync(c.PasswordRef.Key, ct);
        try
        {
            var ok = await providers.For(c.Kind).TestConnectionAsync(c, pw, ct);
            await audit.Record("dbconnection.test", "database_connection", id,
                user.Identity?.Name, ok ? "ok" : "error", null, ct);
            return new TestConnectionResult(ok, ok ? null : "probe returned false");
        }
        catch (Exception ex) when (ex is not AppException)
        {
            await audit.Record("dbconnection.test", "database_connection", id,
                user.Identity?.Name, "error", new { ex.Message }, ct);
            return new TestConnectionResult(false, ex.Message);
        }
    }
}
