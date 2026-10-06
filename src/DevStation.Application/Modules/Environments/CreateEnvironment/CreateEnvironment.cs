using System.Security.Claims;
using DevStation.Application.Shared.Auditing;
using DevStation.Application.Shared.Persistence;
using DevStation.Application.Shared.Results;
using DevStation.Domain.Entities;

namespace DevStation.Application.Modules.Environments.CreateEnvironment;

public sealed record EnvVariableRequest
{
    public string Key { get; init; } = "";
    public string? Value { get; init; }
    public bool Secret { get; init; }
}

public sealed record CreateEnvironmentRequest
{
    public string Name { get; init; } = "";
    public string? Description { get; init; }
    public string Type { get; init; } = "dev";
    public string? BaseUrl { get; init; }
    public string? HealthCheckUrl { get; init; }
    public string? GitBranch { get; init; }
    public string? GitCommit { get; init; }
    public string? DeploymentProvider { get; init; }
    public string? Status { get; init; }
    public List<EnvVariableRequest>? Variables { get; init; }
    public int Order { get; init; }
}

public sealed class CreateEnvironmentHandler(IDevStationDb db, IAuditWriter audit)
{
    public async Task<EnvironmentDto> Handle(CreateEnvironmentRequest r,
        ClaimsPrincipal user, CancellationToken ct)
    {
        var errors = new List<string>();
        EnvironmentValidator.Check(errors, r.Name, r.Type, r.BaseUrl, r.HealthCheckUrl);
        VarCheck(errors, r.Variables);
        if (errors.Count > 0)
            throw new AppException("VALIDATION_ERROR", 400, string.Join("; ", errors));

        var e = new AppEnvironment
        {
            Name = r.Name.Trim(),
            Description = r.Description,
            Type = r.Type,
            BaseUrl = r.BaseUrl,
            HealthCheckUrl = r.HealthCheckUrl,
            GitBranch = r.GitBranch,
            GitCommit = r.GitCommit,
            DeploymentProvider = r.DeploymentProvider,
            Status = string.IsNullOrWhiteSpace(r.Status) ? "unknown" : r.Status,
            Variables = Map(r.Variables),
            Order = r.Order,
        };
        db.Environments.Add(e);
        await db.SaveChangesAsync(ct);
        await audit.Record("environment.create", "environment", e.Id,
            user.Identity?.Name, "ok", new { e.Name }, ct);
        return EnvironmentDto.Of(e, "unknown");
    }

    internal static void VarCheck(List<string> errors, List<EnvVariableRequest>? vars)
    {
        foreach (var v in vars ?? [])
            if (string.IsNullOrWhiteSpace(v.Key) || v.Key.Length > 200)
                errors.Add("variable keys must be 1-200 characters");
    }

    internal static List<EnvironmentVariable> Map(List<EnvVariableRequest>? vars)
        => (vars ?? [])
            .Where(v => !string.IsNullOrWhiteSpace(v.Key))
            .Select(v => new EnvironmentVariable(v.Key.Trim(), v.Value, v.Secret))
            .ToList();
}
