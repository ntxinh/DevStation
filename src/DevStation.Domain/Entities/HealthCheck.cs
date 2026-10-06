namespace DevStation.Domain.Entities;

public class HealthCheck
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? EnvironmentId { get; set; }
    public Guid? ServiceId { get; set; }
    public string Name { get; set; } = "";
    public string Kind { get; set; } = "http"; // http|tcp|database|container
    public string Target { get; set; } = "";   // url | host:port | connection name | container id
    public int IntervalSeconds { get; set; } = 60;
    public int TimeoutSeconds { get; set; } = 10;
    public bool Enabled { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
