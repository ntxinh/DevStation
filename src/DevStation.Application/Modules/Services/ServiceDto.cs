using DevStation.Domain.Entities;

namespace DevStation.Application.Modules.Services;

public sealed record ServiceDto(Guid Id, string Name, string? Description, string? Type,
    string? Url, string? ContainerId, string? Version, string Status,
    Guid? EnvironmentId, string? EnvironmentName, DateTimeOffset UpdatedAt)
{
    public static ServiceDto Of(Service s, string? envName = null) => new(
        s.Id, s.Name, s.Description, s.Type, s.Url, s.ContainerId, s.Version,
        s.Status.ToString().ToLowerInvariant(), s.EnvironmentId, envName, s.UpdatedAt);
}
