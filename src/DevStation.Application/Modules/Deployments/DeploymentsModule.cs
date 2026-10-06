using DevStation.Application.Modules.Deployments.CancelDeployment;
using DevStation.Application.Modules.Deployments.GetDeployment;
using DevStation.Application.Modules.Deployments.ListDeployments;
using DevStation.Application.Modules.Deployments.StartDeployment;
using DevStation.Application.Shared.Persistence;
using DevStation.Domain.Entities;
using DevStation.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DevStation.Application.Modules.Deployments;

/// <summary>Spec §19 casing: queued|running|succeeded|failed|cancelled.</summary>
public sealed record DeploymentDto(
    Guid Id, Guid? ProjectId, string? ProjectName,
    Guid? EnvironmentId, string? EnvironmentName,
    string? Commit, string? Version, string Status, long? DurationMs,
    DateTimeOffset CreatedAt, DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt, IReadOnlyList<string> Logs)
{
    public static DeploymentDto Of(Deployment d, string? projectName,
        string? envName, IReadOnlyList<string>? logs = null) => new(
        d.Id, d.ProjectId, projectName, d.EnvironmentId, envName,
        d.Commit, d.Version, d.Status.ToString().ToLowerInvariant(),
        d.FinishedAt is { } f ? (long)(f - (d.StartedAt ?? d.CreatedAt)).TotalMilliseconds : null,
        d.CreatedAt, d.StartedAt, d.FinishedAt, logs ?? []);
}

public static class DeploymentsModule
{
    public static IServiceCollection AddDeploymentsModule(this IServiceCollection s) => s
        .AddScoped<ListDeploymentsHandler>()
        .AddScoped<GetDeploymentHandler>()
        .AddScoped<StartDeploymentHandler>()
        .AddScoped<CancelDeploymentHandler>();

    /// <summary>Single-deployment read with env/project names (left joins —
    /// both FKs are nullable).</summary>
    internal static async Task<DeploymentDto?> Detail(IDevStationDb db, Guid id,
        IReadOnlyList<string>? logs, CancellationToken ct)
    {
        var d = await db.Deployments.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (d is null) return null;
        var project = d.ProjectId is { } p
            ? await db.Projects.Where(x => x.Id == p).Select(x => x.Name).FirstOrDefaultAsync(ct)
            : null;
        var env = d.EnvironmentId is { } e
            ? await db.Environments.Where(x => x.Id == e).Select(x => x.Name).FirstOrDefaultAsync(ct)
            : null;
        return DeploymentDto.Of(d, project, env, logs);
    }
}
