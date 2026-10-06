using System.Security.Claims;
using DevStation.Application.Shared.Auditing;
using DevStation.Application.Shared.Persistence;
using DevStation.Application.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace DevStation.Application.Modules.Projects.DeleteProject;

public sealed class DeleteProjectHandler(IDevStationDb db, IAuditWriter audit)
{
    public async Task Handle(Guid id, ClaimsPrincipal user, CancellationToken ct)
    {
        var p = await db.Projects.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new AppException("NOT_FOUND", 404, "Project not found");
        db.Projects.Remove(p);
        await db.SaveChangesAsync(ct);
        await audit.Record("project.delete", "project", id,
            user.Identity?.Name, "ok", new { p.Name }, ct);
    }
}
