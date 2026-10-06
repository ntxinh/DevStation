using DevStation.Domain.ValueObjects;

namespace DevStation.Domain.Entities;

public class DatabaseConnection
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? EnvironmentId { get; set; }
    public string Name { get; set; } = "";
    public string Kind { get; set; } = "postgresql"; // postgresql|sqlserver → provider resolver key
    public string Host { get; set; } = "";
    public int Port { get; set; }
    public string Database { get; set; } = "";
    public string Username { get; set; } = "";
    public SecretRef? PasswordRef { get; set; } // secret value never stored here
    public bool ReadOnly { get; set; } = true;  // guardrail: writes need opt-out + confirm
    public int MaxRows { get; set; } = 1000;
    public int TimeoutSeconds { get; set; } = 30;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
