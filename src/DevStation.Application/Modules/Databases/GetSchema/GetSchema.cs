using DevStation.Application.Shared.Persistence;
using DevStation.Application.Shared.Providers;
using DevStation.Application.Shared.Results;
using DevStation.Application.Shared.Secrets;
using Microsoft.EntityFrameworkCore;

namespace DevStation.Application.Modules.Databases.GetSchema;

public sealed class GetSchemaHandler(IDevStationDb db,
    IEnumerable<IDatabaseProvider> providers, ISecretStore secrets)
{
    public async Task<DatabaseSchema> Handle(Guid id, CancellationToken ct)
    {
        var c = await db.DatabaseConnections.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new AppException("NOT_FOUND", 404, "Connection not found");
        var pw = c.PasswordRef is null ? null
            : await secrets.GetAsync(c.PasswordRef.Key, ct);
        try
        {
            return await providers.For(c.Kind).GetSchemaAsync(c, pw, ct);
        }
        catch (Exception ex) when (ex is not AppException)
        {
            throw new AppException("SCHEMA_FAILED", 400, ex.Message);
        }
    }
}
