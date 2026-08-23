namespace Marketplace.Infrastructure.Embeddings;

public interface IEmbeddingService
{
    Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken);
}
