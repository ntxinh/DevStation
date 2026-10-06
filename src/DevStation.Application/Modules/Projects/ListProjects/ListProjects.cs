using DevStation.Application.Shared.Persistence;
using DevStation.Application.Shared.Providers;
using Microsoft.EntityFrameworkCore;

namespace DevStation.Application.Modules.Projects.ListProjects;

public sealed class ListProjectsHandler(IDevStationDb db, IGitProvider git)
{
    public async Task<List<ProjectDto>> Handle(string? q, CancellationToken ct)
    {
        var query = db.Projects.AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(p => p.Name.ToLower().Contains(q.ToLower()));
        var projects = await query.OrderBy(p => p.Name).ToListAsync(ct);

        // ponytail: sequential best-effort git enrichment — N+1 opens fine at devscale
        var dtos = new List<ProjectDto>(projects.Count);
        foreach (var p in projects)
        {
            string? branch = null;
            bool? dirty = null;
            if (p.LocalPath is { Length: > 0 })
            {
                try
                {
                    var s = git.GetStatus(p.LocalPath);
                    branch = s.Branch;
                    dirty = s.Dirty;
                }
                catch { /* not a repo / forbidden — leave null */ }
            }
            dtos.Add(ProjectDto.Of(p, branch, dirty));
        }
        return dtos;
    }
}
