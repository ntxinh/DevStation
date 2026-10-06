using System.Security.Claims;
using DevStation.Application.Modules.Projects.CreateProject;
using DevStation.Application.Shared.Auditing;
using DevStation.Application.Shared.Persistence;
using DevStation.Application.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace DevStation.Application.Modules.Projects.UpdateProject;

public sealed record UpdateProjectRequest
{
    public string? Name { get; init; }
    public string? Description { get; init; }
    public string? RepositoryUrl { get; init; }
    public string? LocalPath { get; init; }
    public string? DefaultBranch { get; init; }
    public string? Technology { get; init; }
    public List<string>? Tags { get; init; }
}

public sealed class UpdateProjectHandler(IDevStationDb db, IAuditWriter audit)
{
    public async Task<ProjectDto> Handle(Guid id, UpdateProjectRequest r, ClaimsPrincipal user, CancellationToken ct)
    {
        var p = await db.Projects.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new AppException("NOT_FOUND", 404, "Project not found");

        // ponytail: null = unchanged — no way to clear a field via PATCH
        var name = r.Name ?? p.Name;
        var repo = r.RepositoryUrl ?? p.RepositoryUrl;
        var path = r.LocalPath ?? p.LocalPath;
        var errors = new List<string>();
        CreateProjectValidator.Check(errors, name, repo, path);
        if (errors.Count > 0)
            throw new AppException("VALIDATION_ERROR", 400, string.Join("; ", errors));

        p.Name = name.Trim();
        p.Description = r.Description ?? p.Description;
        p.RepositoryUrl = repo;
        p.LocalPath = path;
        p.DefaultBranch = r.DefaultBranch ?? p.DefaultBranch;
        p.Technology = r.Technology ?? p.Technology;
        p.Tags = r.Tags ?? p.Tags;
        p.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        await audit.Record("project.update", "project", p.Id,
            user.Identity?.Name, "ok", null, ct);
        return ProjectDto.Of(p);
    }
}
