using DevStation.Application.Modules.Health;
using DevStation.Application.Shared.Persistence;
using DevStation.Application.Shared.Results;
using DevStation.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DevStation.Application.Modules.Environments.GetEnvironment;

public sealed record ServiceDto(Guid Id, string Name, string? Description, string? Type,
    string? Url, string? Version, string Status)
{
    public static ServiceDto Of(Service s) => new(
        s.Id, s.Name, s.Description, s.Type, s.Url, s.Version,
        s.Status.ToString().ToLowerInvariant());
}

public sealed record DeploymentInfo(string? Version, string? Commit,
    DateTimeOffset? DeployedAt);

public sealed record EnvironmentDetailDto(EnvironmentDto Environment,
    List<ServiceDto> Services, List<HealthCheckDto> HealthChecks,
    DeploymentInfo? Deployment);

public sealed class GetEnvironmentHandler(IDevStationDb db)
{
    public async Task<EnvironmentDetailDto> Handle(Guid id, CancellationToken ct)
    {
        var e = await db.Environments.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new AppException("NOT_FOUND", 404, "Environment not found");

        var services = await db.Services
            .Where(s => s.EnvironmentId == id)
            .OrderBy(s => s.Name).ToListAsync(ct);

        var checks = await db.HealthChecks
            .Where(h => h.EnvironmentId == id)
            .OrderBy(h => h.Name).ToListAsync(ct);
        var checkIds = checks.Select(h => h.Id).ToList();
        var results = await db.HealthCheckResults
            .Where(r => checkIds.Contains(r.HealthCheckId))
            .ToListAsync(ct);

        var deployment = await db.Deployments
            .Where(d => d.EnvironmentId == id &&
                        d.Status == Domain.Enums.DeploymentStatus.Succeeded)
            .OrderByDescending(d => d.FinishedAt)
            .Select(d => new DeploymentInfo(d.Version, d.Commit, d.FinishedAt))
            .FirstOrDefaultAsync(ct);

        var health = (await EnvironmentsModule.LatestHealthByEnvironment(db, ct))
            .GetValueOrDefault(id, "unknown");

        return new EnvironmentDetailDto(
            EnvironmentDto.Of(e, health),
            services.Select(ServiceDto.Of).ToList(),
            checks.Select(h => HealthCheckDto.Of(h,
                results.Where(r => r.HealthCheckId == h.Id).MaxBy(r => r.CheckedAt)))
                .ToList(), deployment);
    }
}
