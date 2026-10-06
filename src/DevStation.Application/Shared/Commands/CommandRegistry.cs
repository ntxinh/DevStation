using DevStation.Application.Shared.Persistence;
using DevStation.Application.Shared.Providers;
using DevStation.Application.Shared.Security;
using Microsoft.EntityFrameworkCore;

namespace DevStation.Application.Shared.Commands;

/// <summary>
/// Materializes the command set live on every call (no cache): static
/// navigation seeds for every nav route, plus entity commands read from the
/// db and the container runtime — Open &lt;name&gt;, and actions
/// (restart service/container, trigger job, deploy project → env).
/// </summary>
public sealed class CommandRegistry(IDevStationDb db, IContainerProvider containers)
{
    /// <summary>Route list mirrors the sidebar nav (apps/web Nav.tsx).</summary>
    private static readonly (string Id, string Title, string Route)[] Nav =
    [
        ("nav.dashboard", "Go to Dashboard", "/dashboard"),
        ("nav.projects", "Go to Projects", "/projects"),
        ("nav.environments", "Go to Environments", "/environments"),
        ("nav.services", "Go to Services", "/services"),
        ("nav.containers", "Go to Containers", "/containers"),
        ("nav.databases", "Go to Databases", "/databases"),
        ("nav.jobs", "Go to Jobs", "/jobs"),
        ("nav.deployments", "Go to Deployments", "/deployments"),
        ("nav.logs", "Go to Logs", "/logs"),
        ("nav.health", "Go to Health", "/health"),
        ("nav.integrations", "Go to Integrations", "/integrations"),
        ("nav.audit", "Go to Audit Log", "/audit"),
        ("nav.settings", "Go to Settings", "/settings"),
    ];

    public async Task<IReadOnlyList<DevStationCommand>> AllAsync(CancellationToken ct)
    {
        var list = new List<DevStationCommand>(
            Nav.Select(n => new DevStationCommand(n.Id, n.Title, "navigation",
                n.Route, "read", false)));

        var projects = await db.Projects.AsNoTracking()
            .Select(p => new { p.Id, p.Name }).ToListAsync(ct);
        var envs = await db.Environments.AsNoTracking()
            .Select(e => new { e.Id, e.Name, e.Type }).ToListAsync(ct);
        var services = await db.Services.AsNoTracking()
            .Select(s => new { s.Id, s.Name, s.ContainerId }).ToListAsync(ct);
        var jobs = await db.JobDefinitions.AsNoTracking()
            .Select(j => new { j.Id, j.Name }).ToListAsync(ct);

        list.AddRange(projects.Select(p => new DevStationCommand(
            $"project.open:{p.Id}", $"Open project {p.Name}", "navigation",
            $"/projects/{p.Id}", "read", false)));
        list.AddRange(envs.Select(e => new DevStationCommand(
            $"env.open:{e.Id}", $"Open environment {e.Name}", "navigation",
            $"/environments/{e.Id}", "read", false)));
        list.AddRange(services.Select(s => new DevStationCommand(
            $"service.open:{s.Id}", $"Open service {s.Name}", "navigation",
            "/services", "read", false)));
        list.AddRange(jobs.Select(j => new DevStationCommand(
            $"job.open:{j.Id}", $"Open job {j.Name}", "navigation",
            $"/jobs/{j.Id}", "read", false)));

        list.AddRange(services.Where(s => !string.IsNullOrWhiteSpace(s.ContainerId))
            .Select(s => new DevStationCommand(
                $"service.restart:{s.Id}", $"Restart service {s.Name}", "action",
                $"service.restart:{s.Id}", "execute", true)));
        list.AddRange(jobs.Select(j => new DevStationCommand(
            $"job.trigger:{j.Id}", $"Trigger job {j.Name}", "action",
            $"job.trigger:{j.Id}", "execute", true)));
        list.AddRange(projects.SelectMany(p => envs.Select(e => new DevStationCommand(
            $"deploy:{p.Id}:{e.Id}", $"Deploy {p.Name} to {e.Name}", "action",
            $"deploy:{p.Id}:{e.Id}", e.Type == "production" ? "deploy" : "execute",
            true, new Dictionary<string, string> { ["commit"] = "string?" }))));

        try
        {
            var live = await containers.ListAsync(ct);
            list.AddRange(live.Select(c => new DevStationCommand(
                $"container.open:{c.Id}", $"Open container {c.Name}", "navigation",
                $"/containers/{c.Id}", "read", false)));
            list.AddRange(live.Select(c => new DevStationCommand(
                $"container.restart:{c.Id}", $"Restart container {c.Name}", "action",
                $"container.restart:{c.Id}", "execute", true)));
        }
        catch (Exception) when (!ct.IsCancellationRequested) { /* runtime unavailable — no container commands */ }

        return list;
    }

    /// <summary>Commands visible at the caller's tier.</summary>
    public async Task<IReadOnlyList<DevStationCommand>> ForTierAsync(string? tier, CancellationToken ct)
    {
        var rank = tier is null ? 0 : Tiers.Rank(tier);
        return (await AllAsync(ct))
            .Where(c => Tiers.Rank(c.RequiredTier) <= rank).ToList();
    }

    public async Task<DevStationCommand?> FindAsync(string id, CancellationToken ct)
        => (await AllAsync(ct)).FirstOrDefault(c =>
            string.Equals(c.Id, id, StringComparison.Ordinal));
}
