namespace DevStation.Domain.Entities;

public class Notification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Type { get; set; } = ""; // deployment_failed|health_unhealthy|job_failed|container_stopped
    public string Title { get; set; } = "";
    public string? Message { get; set; }
    public string? Severity { get; set; } // info|warn|error
    /// <summary>Redacted-at-write context json (secrets can never land here).</summary>
    public string? Metadata { get; set; }
    public string? Link { get; set; }
    public bool Read { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
