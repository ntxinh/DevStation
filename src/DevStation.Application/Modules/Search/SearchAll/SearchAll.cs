using DevStation.Application.Shared.Persistence;
using DevStation.Application.Shared.Providers;
using Microsoft.EntityFrameworkCore;

namespace DevStation.Application.Modules.Search.SearchAll;

public sealed record SearchHit(string Id, string Name, string? Detail,
    string? Status);

public sealed class SearchGroups
{
    public List<SearchHit> Projects { get; set; } = [];
    public List<SearchHit> Environments { get; set; } = [];
    public List<SearchHit> Services { get; set; } = [];
    public List<SearchHit> Jobs { get; set; } = [];
    public List<SearchHit> Databases { get; set; } = [];
    public List<SearchHit> Deployments { get; set; } = [];
    public List<SearchHit> Integrations { get; set; } = [];
    /// <summary>Best-effort live container names (not in the entity contract
    /// but returned so /logs and dashboards can deep-link).</summary>
    public List<SearchHit> Containers { get; set; } = [];
}

/// <summary>Grouped global search — top 5 per area, case-insensitive
/// substring on name/description-ish fields. q shorter than 2 chars → all
/// groups empty. Container names come from the runtime provider,
/// best-effort.</summary>
public sealed class SearchAllHandler(IDevStationDb db,
    IContainerProvider containers)
{
    private const int Top = 5;

    public async Task<SearchGroups> Handle(string? q, CancellationToken ct)
    {
        var groups = new SearchGroups();
        if (q is null || q.Trim().Length < 2) return groups;
        var needle = q.Trim().ToLowerInvariant();

        groups.Projects = await db.Projects.AsNoTracking()
            .Where(p => p.Name.ToLower().Contains(needle)
                || (p.Description != null && p.Description.ToLower().Contains(needle)))
            .OrderBy(p => p.Name).Take(Top)
            .Select(p => new SearchHit(p.Id.ToString(), p.Name, p.Description, null))
            .ToListAsync(ct);

        groups.Environments = await db.Environments.AsNoTracking()
            .Where(e => e.Name.ToLower().Contains(needle)
                || (e.Description != null && e.Description.ToLower().Contains(needle)))
            .OrderBy(e => e.Name).Take(Top)
            .Select(e => new SearchHit(e.Id.ToString(), e.Name, e.Type, e.Status))
            .ToListAsync(ct);

        groups.Services = await db.Services.AsNoTracking()
            .Where(s => s.Name.ToLower().Contains(needle)
                || (s.Description != null && s.Description.ToLower().Contains(needle)))
            .OrderBy(s => s.Name).Take(Top)
            .Select(s => new SearchHit(s.Id.ToString(), s.Name, s.Type,
                s.Status.ToString().ToLower()))
            .ToListAsync(ct);

        groups.Jobs = await db.JobDefinitions.AsNoTracking()
            .Where(j => j.Name.ToLower().Contains(needle)
                || (j.Description != null && j.Description.ToLower().Contains(needle)))
            .OrderBy(j => j.Name).Take(Top)
            .Select(j => new SearchHit(j.Id.ToString(), j.Name, j.Description,
                j.Status.ToString().ToLower()))
            .ToListAsync(ct);

        groups.Databases = await db.DatabaseConnections.AsNoTracking()
            .Where(d => d.Name.ToLower().Contains(needle)
                || d.Database.ToLower().Contains(needle)
                || d.Host.ToLower().Contains(needle))
            .OrderBy(d => d.Name).Take(Top)
            .Select(d => new SearchHit(d.Id.ToString(), d.Name,
                $"{d.Host}:{d.Port}/{d.Database}", d.Kind))
            .ToListAsync(ct);

        groups.Deployments = await db.Deployments.AsNoTracking()
            .Where(d => (d.Commit != null && d.Commit.ToLower().Contains(needle))
                || (d.Version != null && d.Version.ToLower().Contains(needle)))
            .OrderByDescending(d => d.CreatedAt).Take(Top)
            .Select(d => new SearchHit(d.Id.ToString(),
                d.Version ?? d.Commit ?? d.Id.ToString(), d.Commit,
                d.Status.ToString().ToLower()))
            .ToListAsync(ct);

        groups.Integrations = await db.Integrations.AsNoTracking()
            .Where(i => i.Name.ToLower().Contains(needle)
                || i.Type.ToLower().Contains(needle))
            .OrderBy(i => i.Name).Take(Top)
            .Select(i => new SearchHit(i.Id.ToString(), i.Name, i.Type,
                i.Status.ToString().ToLower()))
            .ToListAsync(ct);
        try
        {
            groups.Containers = (await containers.ListAsync(ct))
                .Where(c => c.Name.Contains(needle, StringComparison.OrdinalIgnoreCase)
                    || c.Image.Contains(needle, StringComparison.OrdinalIgnoreCase))
                .Take(Top)
                .Select(c => new SearchHit(c.Id, c.Name, c.Image, c.State))
                .ToList();
        }
        catch (Exception) when (!ct.IsCancellationRequested) { /* runtime unavailable — containers group stays empty */ }

        return groups;
    }
}
