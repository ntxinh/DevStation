using System.Security.Claims;
using DevStation.Application.Modules.Environments.CreateEnvironment;
using DevStation.Application.Shared.Auditing;
using DevStation.Application.Shared.Persistence;
using DevStation.Application.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace DevStation.Application.Modules.Environments.UpdateEnvironment;

public sealed record UpdateEnvironmentRequest
{
    public string? Name { get; init; }
    public string? Description { get; init; }
    public string? Type { get; init; }
    public string? BaseUrl { get; init; }
    public string? HealthCheckUrl { get; init; }
    public string? GitBranch { get; init; }
    public string? GitCommit { get; init; }
    public string? DeploymentProvider { get; init; }
    public string? Status { get; init; }
    public List<EnvVariableRequest>? Variables { get; init; }
    public int? Order { get; init; }
}

public sealed class UpdateEnvironmentHandler(IDevStationDb db, IAuditWriter audit)
{
    public async Task<EnvironmentDto> Handle(Guid id, UpdateEnvironmentRequest r,
        ClaimsPrincipal user, CancellationToken ct)
    {
        var e = await db.Environments.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new AppException("NOT_FOUND", 404, "Environment not found");

        // ponytail: null = unchanged — no way to clear a field via PATCH;
        // variables are a whole-list replace, send null for a secret's value to keep it
        var name = r.Name ?? e.Name;
        var type = r.Type ?? e.Type;
        var baseUrl = r.BaseUrl ?? e.BaseUrl;
        var healthCheckUrl = r.HealthCheckUrl ?? e.HealthCheckUrl;
        var errors = new List<string>();
        EnvironmentValidator.Check(errors, name, type, baseUrl, healthCheckUrl);
        CreateEnvironmentHandler.VarCheck(errors, r.Variables);
        if (errors.Count > 0)
            throw new AppException("VALIDATION_ERROR", 400, string.Join("; ", errors));

        e.Name = name.Trim();
        e.Description = r.Description ?? e.Description;
        e.Type = type;
        e.BaseUrl = baseUrl;
        e.HealthCheckUrl = healthCheckUrl;
        e.GitBranch = r.GitBranch ?? e.GitBranch;
        e.GitCommit = r.GitCommit ?? e.GitCommit;
        e.DeploymentProvider = r.DeploymentProvider ?? e.DeploymentProvider;
        e.Status = r.Status ?? e.Status;
        if (r.Variables is not null)
            e.Variables = MergeSecrets(e.Variables, r.Variables);
        e.Order = r.Order ?? e.Order;
        e.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        await audit.Record("environment.update", "environment", e.Id,
            user.Identity?.Name, "ok", null, ct);
        var health = (await EnvironmentsModule.LatestHealthByEnvironment(db, ct))
            .GetValueOrDefault(id, "unknown");
        return EnvironmentDto.Of(e, health);
    }

    // secret vars arrive without values on read — null value on a secret keeps the stored one
    private static List<Domain.Entities.EnvironmentVariable> MergeSecrets(
        List<Domain.Entities.EnvironmentVariable> existing, List<EnvVariableRequest> incoming)
        => incoming.Where(v => !string.IsNullOrWhiteSpace(v.Key)).Select(v =>
        {
            var value = v.Value;
            if (v.Secret && value is null)
                value = existing.FirstOrDefault(x => x.Key == v.Key.Trim())?.Value;
            return new Domain.Entities.EnvironmentVariable(v.Key.Trim(), value, v.Secret);
        }).ToList();
}
