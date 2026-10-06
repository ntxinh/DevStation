namespace DevStation.Domain.Entities;

public class QueryHistoryEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? ConnectionId { get; set; }
    public string Sql { get; set; } = ""; // truncated 4k, param values never stored
    public int? RowCount { get; set; }
    public int? DurationMs { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
    public DateTimeOffset ExecutedAt { get; set; } = DateTimeOffset.UtcNow;
}
