namespace Marketplace.Domain.Operations;

/// <summary>Append-only operational/analytics evidence. Payload is deliberately small and non-PII.</summary>
public sealed class ProductEvent
{
    private ProductEvent() { }
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string SubjectType { get; private set; } = string.Empty;
    public Guid SubjectId { get; private set; }
    public Guid? ActorId { get; private set; }
    public string? Metadata { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }

    public static ProductEvent Create(string name, string subjectType, Guid subjectId, Guid? actorId,
        string? metadata, DateTimeOffset occurredAt) => new()
    {
        Id = Guid.NewGuid(), Name = Required(name, 100), SubjectType = Required(subjectType, 60),
        SubjectId = subjectId, ActorId = actorId,
        Metadata = string.IsNullOrWhiteSpace(metadata) ? null : TrimMetadata(metadata),
        OccurredAt = occurredAt
    };

    private static string Required(string value, int maxLength)
    {
        string clean = value?.Trim() ?? string.Empty;
        if (clean.Length == 0 || clean.Length > maxLength) throw new ArgumentException("A valid event value is required.");
        return clean;
    }
    private static string TrimMetadata(string value)
    {
        string clean = value.Trim();
        return clean.Length <= 2000 ? clean : clean[..2000];
    }
}
