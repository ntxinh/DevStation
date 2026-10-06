using DevStation.Domain.Entities;

namespace DevStation.Application.Modules.Databases;

public sealed record ConnectionDto(Guid Id, string Name, string Kind,
    Guid? EnvironmentId, string? EnvironmentType, string Host, int Port,
    string Database, string Username, bool HasPassword, bool ReadOnly,
    int MaxRows, int TimeoutSeconds,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt)
{
    /// <summary>Password never leaves — only its presence flag.</summary>
    public static ConnectionDto Of(DatabaseConnection c, string? envType) => new(
        c.Id, c.Name, c.Kind, c.EnvironmentId, envType, c.Host, c.Port,
        c.Database, c.Username, c.PasswordRef is not null, c.ReadOnly,
        c.MaxRows, c.TimeoutSeconds, c.CreatedAt, c.UpdatedAt);
}

public static class ConnectionValidator
{
    public static readonly string[] Kinds = ["postgresql", "sqlserver"];

    public static void Check(List<string> errors, string? name, string? kind,
        string? host, int port, string? database, string? username,
        int maxRows, int timeoutSeconds)
    {
        if (name?.Trim() is null or "" or { Length: > 200 })
            errors.Add("name must be 1-200 characters");
        if (kind is not null && !Kinds.Contains(kind))
            errors.Add($"kind must be one of: {string.Join("|", Kinds)}");
        if (host?.Trim() is null or "" or { Length: > 255 })
            errors.Add("host must be 1-255 characters");
        if (port is < 1 or > 65535)
            errors.Add("port must be 1-65535");
        if (database?.Trim() is null or "" or { Length: > 200 })
            errors.Add("database must be 1-200 characters");
        if (username?.Trim() is null or "" or { Length: > 200 })
            errors.Add("username must be 1-200 characters");
        if (maxRows is < 1 or > 10_000)
            errors.Add("maxRows must be 1-10000");
        if (timeoutSeconds is < 1 or > 300)
            errors.Add("timeoutSeconds must be 1-300");
    }
}
