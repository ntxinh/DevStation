using System.Text.Json.Serialization;
using DevStation.Domain.Entities;

namespace DevStation.Application.Modules.Environments;

public sealed record EnvVariableDto(
    string Key,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Value,
    bool Secret);

public sealed record EnvironmentDto(Guid Id, string Name, string? Description, string Type,
    string? BaseUrl, string? HealthCheckUrl, string? GitBranch, string? GitCommit,
    string? DeploymentProvider, string Status, List<EnvVariableDto> Variables,
    int Order, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, string Health)
{
    /// <summary>health is the aggregate of the env's latest health-check results.</summary>
    public static EnvironmentDto Of(AppEnvironment e, string health) => new(
        e.Id, e.Name, e.Description, e.Type, e.BaseUrl, e.HealthCheckUrl,
        e.GitBranch, e.GitCommit, e.DeploymentProvider, e.Status,
        // secrets never leave the server — value omitted entirely
        e.Variables.Select(v => new EnvVariableDto(v.Key, v.Secret ? null : v.Value, v.Secret)).ToList(),
        e.Order, e.CreatedAt, e.UpdatedAt, health);
}
