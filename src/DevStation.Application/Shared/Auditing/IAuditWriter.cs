namespace DevStation.Application.Shared.Auditing;

public interface IAuditWriter
{
    Task Record(string action, string resource, Guid? resourceId, string? user,
        string result, object? metadata, CancellationToken ct = default);
}
