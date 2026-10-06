using DevStation.Application.Shared.Persistence;
using DevStation.Application.Shared.Providers;
using DevStation.Application.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace DevStation.Application.Modules.Projects.GetGitStatus;

public sealed class GetGitStatusHandler(IDevStationDb db, IGitProvider git)
{
    public async Task<GitStatus> Handle(Guid id, CancellationToken ct)
    {
        var p = await db.Projects.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new AppException("NOT_FOUND", 404, "Project not found");
        if (string.IsNullOrEmpty(p.LocalPath))
            throw new AppException("GIT_NO_PATH", 400, "Project has no localPath");
        return git.GetStatus(p.LocalPath);
    }
}
