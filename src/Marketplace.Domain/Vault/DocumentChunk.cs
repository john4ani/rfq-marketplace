namespace Marketplace.Domain.Vault;

/// <summary>
/// One overlapping text chunk of a vault document with its embedding. The embedding is a CLR float
/// array; the persistence layer maps it to a pgvector "vector" column.
/// </summary>
public sealed class DocumentChunk
{
    private DocumentChunk() { }

    public Guid Id { get; private set; }
    public Guid DocumentId { get; private set; }
    public int ChunkIndex { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public float[]? Embedding { get; private set; }
    public int? TokenCount { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static DocumentChunk Create(Guid documentId, int chunkIndex, string content, float[]? embedding, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        DocumentId = documentId,
        ChunkIndex = chunkIndex,
        Content = Required(content, "Conținutul fragmentului este obligatoriu."),
        Embedding = embedding,
        TokenCount = embedding?.Length,
        CreatedAt = now
    };

    private static string Required(string value, string message) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException(message) : value;
}
