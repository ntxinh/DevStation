using System.Security.Claims;
using DevStation.Application.Shared.Auditing;
using DevStation.Application.Shared.Persistence;
using DevStation.Application.Shared.Results;
using DevStation.Domain.Entities;

namespace DevStation.Application.Modules.Projects.CreateProject;

public sealed record CreateProjectRequest
{
    public string Name { get; init; } = "";
    public string? Description { get; init; }
    public string? RepositoryUrl { get; init; }
    public string? LocalPath { get; init; }
    public string? DefaultBranch { get; init; }
    public string? Technology { get; init; }
    public List<string>? Tags { get; init; }
}

public static class CreateProjectValidator
{
    public static List<string> Validate(CreateProjectRequest r)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(r.Name))
            errors.Add("name is required");
        Check(errors, r.Name, r.RepositoryUrl, r.LocalPath);
        return errors;
    }

    /// <summary>Field rules shared with update (call with the merged entity values).</summary>
    public static void Check(List<string> errors, string? name, string? repoUrl, string? localPath)
    {
        // validate the stored value — a whitespace name must not trim to "" past validation
        var trimmed = name?.Trim();
        if (trimmed is null or "" or { Length: > 200 })
            errors.Add("name must be 1-200 characters");
        if (repoUrl is { Length: > 0 } u &&
            !(Uri.TryCreate(u, UriKind.Absolute, out var uri) &&
              (uri.Scheme is "http" or "https")))
            errors.Add("repositoryUrl must be an http(s) URL");
        if (localPath is { Length: > 0 } p && !Path.IsPathFullyQualified(p))
            errors.Add("localPath must be an absolute path");
    }
}

public sealed class CreateProjectHandler(IDevStationDb db, IAuditWriter audit)
{
    public async Task<ProjectDto> Handle(CreateProjectRequest r, ClaimsPrincipal user, CancellationToken ct)
    {
        var errors = CreateProjectValidator.Validate(r);
        if (errors.Count > 0)
            throw new AppException("VALIDATION_ERROR", 400, string.Join("; ", errors));

        var p = new Project
        {
            Name = r.Name.Trim(),
            Description = r.Description,
            RepositoryUrl = r.RepositoryUrl,
            LocalPath = r.LocalPath,
            DefaultBranch = r.DefaultBranch,
            Technology = r.Technology,
            Tags = r.Tags ?? [],
        };
        db.Projects.Add(p);
        await db.SaveChangesAsync(ct);
        await audit.Record("project.create", "project", p.Id,
            user.Identity?.Name, "ok", new { p.Name }, ct);
        return ProjectDto.Of(p);
    }
}
