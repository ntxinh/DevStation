using System.Security.Claims;
using DevStation.Application.Shared.Auditing;
using DevStation.Application.Shared.Persistence;
using DevStation.Application.Shared.Results;
using DevStation.Domain.Entities;

namespace DevStation.Application.Modules.Health.CreateHealthCheck;

public sealed record CreateHealthCheckRequest
{
    public string Name { get; init; } = "";
    public string Kind { get; init; } = "http";
    public string Target { get; init; } = "";
    public Guid? EnvironmentId { get; init; }
    public Guid? ServiceId { get; init; }
    public int IntervalSeconds { get; init; } = 60;
    public int TimeoutSeconds { get; init; } = 5;
    public bool Enabled { get; init; } = true;
}

public static class HealthCheckValidator
{
    public static readonly string[] Kinds = ["http", "tcp", "database", "container", "custom"];

    public static void Check(List<string> errors, string? name, string? kind,
        string? target, int intervalSeconds, int timeoutSeconds)
    {
        if (name?.Trim() is null or "" or { Length: > 200 })
            errors.Add("name must be 1-200 characters");
        if (kind is { Length: > 0 } k && !Kinds.Contains(k))
            errors.Add($"kind must be one of: {string.Join("|", Kinds)}");
        if (string.IsNullOrWhiteSpace(target))
            errors.Add("target is required");
        if (intervalSeconds is < 5 or > 86400)
            errors.Add("intervalSeconds must be 5-86400");
        if (timeoutSeconds is < 1 or > 300)
            errors.Add("timeoutSeconds must be 1-300");
    }
}

public sealed class CreateHealthCheckHandler(IDevStationDb db, IAuditWriter audit)
{
    public async Task<HealthCheckDto> Handle(CreateHealthCheckRequest r,
        ClaimsPrincipal user, CancellationToken ct)
    {
        var errors = new List<string>();
        HealthCheckValidator.Check(errors, r.Name, r.Kind, r.Target,
            r.IntervalSeconds, r.TimeoutSeconds);
        if (errors.Count > 0)
            throw new AppException("VALIDATION_ERROR", 400, string.Join("; ", errors));

        var h = new HealthCheck
        {
            Name = r.Name.Trim(),
            Kind = r.Kind,
            Target = r.Target.Trim(),
            EnvironmentId = r.EnvironmentId,
            ServiceId = r.ServiceId,
            IntervalSeconds = r.IntervalSeconds,
            TimeoutSeconds = r.TimeoutSeconds,
            Enabled = r.Enabled,
        };
        db.HealthChecks.Add(h);
        await db.SaveChangesAsync(ct);
        await audit.Record("healthcheck.create", "health_check", h.Id,
            user.Identity?.Name, "ok", new { h.Name, h.Kind }, ct);
        return HealthCheckDto.Of(h);
    }
}
