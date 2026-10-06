using DevStation.Application.Shared.Persistence;
using DevStation.Application.Shared.Providers;
using DevStation.Application.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace DevStation.Application.Modules.Projects.GetRecentCommits;

public sealed class GetRecentCommitsHandler(IDevStationDb db, IGitProvider git)
{
    public async Task<IReadOnlyList<CommitInfo>> Handle(Guid id, int n, CancellationToken ct)
    {
        var p = await db.Projects.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new AppException("NOT_FOUND", 404, "Project not found");
        if (string.IsNullOrEmpty(p.LocalPath))
            throw new AppException("GIT_NO_PATH", 400, "Project has no localPath");
        return git.GetRecentCommits(p.LocalPath, n is > 0 and <= 100 ? n : 10);
    }
}
