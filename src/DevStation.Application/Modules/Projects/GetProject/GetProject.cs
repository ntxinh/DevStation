using DevStation.Application.Shared.Persistence;
using DevStation.Application.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace DevStation.Application.Modules.Projects.GetProject;

public sealed class GetProjectHandler(IDevStationDb db)
{
    public async Task<ProjectDto> Handle(Guid id, CancellationToken ct)
    {
        var p = await db.Projects.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new AppException("NOT_FOUND", 404, "Project not found");
        return ProjectDto.Of(p);
    }
}
