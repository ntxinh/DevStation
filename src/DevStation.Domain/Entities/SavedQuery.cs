namespace DevStation.Domain.Entities;

public class SavedQuery
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? ConnectionId { get; set; }
    public string Name { get; set; } = "";
    public string Sql { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
