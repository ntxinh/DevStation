using DevStation.Application.Shared.Persistence;
using DevStation.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DevStation.Application.Modules.Deployments.ListDeployments;

public sealed class ListDeploymentsHandler(IDevStationDb db)
{
    /// <summary>Newest-first history with env/project names resolved.</summary>
    public async Task<List<DeploymentDto>> Handle(Guid? environmentId,
        Guid? projectId, int? take, CancellationToken ct)
    {
        var query = db.Deployments.AsQueryable();
        if (environmentId is { } e) query = query.Where(d => d.EnvironmentId == e);
        if (projectId is { } p) query = query.Where(d => d.ProjectId == p);

        var rows = await query
            .OrderByDescending(d => d.CreatedAt)
            .Take(Math.Clamp(take ?? 100, 1, 500))
            .ToListAsync(ct);

        var envIds = rows.Select(d => d.EnvironmentId).OfType<Guid>().Distinct().ToList();
        var projIds = rows.Select(d => d.ProjectId).OfType<Guid>().Distinct().ToList();
        var envs = await db.Environments.Where(x => envIds.Contains(x.Id))
            .Select(x => new { x.Id, x.Name }).ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        var projects = await db.Projects.Where(x => projIds.Contains(x.Id))
            .Select(x => new { x.Id, x.Name }).ToDictionaryAsync(x => x.Id, x => x.Name, ct);

        return rows.Select(d => DeploymentDto.Of(d,
            d.ProjectId is { } p && projects.TryGetValue(p, out var pn) ? pn : null,
            d.EnvironmentId is { } e && envs.TryGetValue(e, out var en) ? en : null))
            .ToList();
    }
}
