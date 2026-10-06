namespace DevStation.Domain.Entities;

public class AuditEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public string? User { get; set; }
    public string Action { get; set; } = "";      // e.g. project.create
    public string? Resource { get; set; }
    public string? ResourceId { get; set; }
    public Guid? EnvironmentId { get; set; }
    public string Result { get; set; } = "";      // ok|denied|error
    public string? Metadata { get; set; }         // jsonb — metadata sans secrets
}
