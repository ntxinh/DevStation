using DevStation.Application.Shared.Persistence;
using DevStation.Application.Shared.Results;
using DevStation.Domain.Entities;
using DevStation.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DevStation.Application.Modules.Environments.CompareEnvironments;

public sealed record ComparePair<T>(T A, T B);
public sealed record CompareService(string Name, bool InA, bool InB);
public sealed record CompareVariable(string Key, bool Differs, bool Masked);
public sealed record CompareResult(ComparePair<string?> Version,
    ComparePair<string?> Commit, ComparePair<DateTimeOffset?> DeployedAt,
    List<CompareService> Services, List<CompareVariable> Variables);

/// <summary>Pure diff between two environments — unit-tested, no IO.</summary>
public static class EnvironmentCompare
{
    public static CompareResult Diff(
        AppEnvironment a, AppEnvironment b,
        List<Service> servicesA, List<Service> servicesB,
        List<Deployment> deploymentsA, List<Deployment> deploymentsB)
    {
        var services = servicesA.Select(s => s.Name).Union(servicesB.Select(s => s.Name))
            .OrderBy(n => n)
            .Select(n => new CompareService(n,
                servicesA.Any(s => s.Name == n), servicesB.Any(s => s.Name == n)))
            .ToList();

        var variables = a.Variables.Select(v => v.Key)
            .Union(b.Variables.Select(v => v.Key))
            .OrderBy(k => k)
            .Select(k => DiffVariable(k, a.Variables, b.Variables))
            .ToList();

        var depA = LatestSucceeded(deploymentsA);
        var depB = LatestSucceeded(deploymentsB);
        return new CompareResult(
            new(depA?.Version, depB?.Version),
            new(depA?.Commit ?? a.GitCommit, depB?.Commit ?? b.GitCommit),
            new(depA?.FinishedAt, depB?.FinishedAt),
            services, variables);
    }

    private static Deployment? LatestSucceeded(List<Deployment> deps)
        => deps.Where(d => d.Status == DeploymentStatus.Succeeded)
               .OrderByDescending(d => d.FinishedAt ?? DateTimeOffset.MinValue)
               .FirstOrDefault();

    private static CompareVariable DiffVariable(string key,
        List<EnvironmentVariable> a, List<EnvironmentVariable> b)
    {
        var va = a.FirstOrDefault(v => v.Key == key);
        var vb = b.FirstOrDefault(v => v.Key == key);
        var masked = va?.Secret == true || vb?.Secret == true;
        var differs = masked
            ? (va is not null) != (vb is not null) // secrets: presence only, never value
            : va?.Value != vb?.Value;
        return new CompareVariable(key, differs, masked);
    }
}

public sealed class CompareEnvironmentsHandler(IDevStationDb db)
{
    public async Task<CompareResult> Handle(Guid aId, Guid bId, CancellationToken ct)
    {
        var envs = await db.Environments
            .Where(e => e.Id == aId || e.Id == bId)
            .ToListAsync(ct);
        var a = envs.FirstOrDefault(e => e.Id == aId)
            ?? throw new AppException("NOT_FOUND", 404, "Environment 'a' not found");
        var b = envs.FirstOrDefault(e => e.Id == bId)
            ?? throw new AppException("NOT_FOUND", 404, "Environment 'b' not found");

        var services = await db.Services
            .Where(s => s.EnvironmentId == aId || s.EnvironmentId == bId)
            .ToListAsync(ct);
        var deployments = await db.Deployments
            .Where(d => d.EnvironmentId == aId || d.EnvironmentId == bId)
            .ToListAsync(ct);

        return EnvironmentCompare.Diff(a, b,
            services.Where(s => s.EnvironmentId == aId).ToList(),
            services.Where(s => s.EnvironmentId == bId).ToList(),
            deployments.Where(d => d.EnvironmentId == aId).ToList(),
            deployments.Where(d => d.EnvironmentId == bId).ToList());
    }
}
