namespace DevStation.Domain.Entities;

// named AppEnvironment: `Environment` collides with System.Environment
public class AppEnvironment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string Type { get; set; } = "development"; // development|staging|production — production blocks writes
    public string? BaseUrl { get; set; }
    public List<EnvironmentVariable> Variables { get; set; } = []; // jsonb; secret=true → value masked/compared by presence only
    public int Order { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public record EnvironmentVariable(string Key, string? Value, bool Secret);
