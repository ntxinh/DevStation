using System.Security.Claims;
using DevStation.Application.Shared.Auditing;
using DevStation.Application.Shared.Persistence;
using DevStation.Application.Shared.Providers;
using DevStation.Application.Shared.Results;
using DevStation.Domain.Entities;

namespace DevStation.Application.Modules.Services.CreateService;

public sealed record CreateServiceRequest
{
    public string Name { get; init; } = "";
    public string? Description { get; init; }
    public string? Type { get; init; }
    public string? Url { get; init; }
    public string? ContainerId { get; init; }
    public string? Version { get; init; }
    public Guid? EnvironmentId { get; init; }
}

public sealed class CreateServiceHandler(IDevStationDb db, IAuditWriter audit)
{
    public async Task<ServiceDto> Handle(CreateServiceRequest r,
        ClaimsPrincipal user, CancellationToken ct)
    {
        if (r.Name?.Trim() is null or "" or { Length: > 200 })
            throw new AppException("VALIDATION_ERROR", 400,
                "name must be 1-200 characters");
        if (r.ContainerId is { Length: > 0 } cid)
            ContainerId.Ensure(cid); // throws CONTAINER_ID_INVALID

        var s = new Service
        {
            Name = r.Name.Trim(),
            Description = r.Description,
            Type = r.Type,
            Url = r.Url,
            ContainerId = r.ContainerId,
            Version = r.Version,
            EnvironmentId = r.EnvironmentId,
        };
        db.Services.Add(s);
        await db.SaveChangesAsync(ct);
        await audit.Record("service.create", "service", s.Id,
            user.Identity?.Name, "ok", new { s.Name, s.Type }, ct);
        return ServiceDto.Of(s);
    }
}
