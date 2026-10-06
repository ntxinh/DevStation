using DevStation.Domain.Entities;

namespace DevStation.Application.Modules.Projects;

public sealed record ProjectDto(Guid Id, string Name, string? Description, string? RepositoryUrl,
    string? LocalPath, string? DefaultBranch, string? Technology, List<string> Tags,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, string? GitBranch, bool? GitDirty)
{
    public static ProjectDto Of(Project p, string? branch = null, bool? dirty = null)
        => new(p.Id, p.Name, p.Description, p.RepositoryUrl, p.LocalPath, p.DefaultBranch,
            p.Technology, p.Tags, p.CreatedAt, p.UpdatedAt, branch, dirty);
}
