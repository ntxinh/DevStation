namespace DevStation.Domain.Entities;

// named AppEnvironment: `Environment` collides with System.Environment
public class AppEnvironment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string Type { get; set; } = "dev"; // dev|qa|staging|production
    public string? BaseUrl { get; set; }
    public string? HealthCheckUrl { get; set; }
    public string? GitBranch { get; set; }
    public string? GitCommit { get; set; }
    public string? DeploymentProvider { get; set; }
    public string Status { get; set; } = "unknown"; // manual/deliberate status override (e.g. "maintenance")
    public List<EnvironmentVariable> Variables { get; set; } = []; // jsonb; secret=true → value masked/compared by presence only
    public int Order { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public record EnvironmentVariable(string Key, string? Value, bool Secret);
