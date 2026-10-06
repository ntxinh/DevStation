using DevStation.Domain.Enums;
using DevStation.Domain.ValueObjects;

namespace DevStation.Domain.Entities;

public class Integration
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Type { get; set; } = ""; // github|jira|azure|cloudflare
    public IntegrationStatus Status { get; set; } = IntegrationStatus.NotConfigured;
    public string? Config { get; set; } // jsonb, non-secret settings only
    public SecretRef? CredentialsRef { get; set; }
    public DateTimeOffset? LastSyncAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
