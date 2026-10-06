namespace DevStation.Domain.Entities;

public class Project
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string? RepositoryUrl { get; set; }
    public string? LocalPath { get; set; }
    public string? DefaultBranch { get; set; }
    public string? Technology { get; set; }
    public List<Guid> EnvironmentIds { get; set; } = [];
    public List<string> Tags { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
