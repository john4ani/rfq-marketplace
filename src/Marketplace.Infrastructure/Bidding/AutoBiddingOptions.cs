namespace Marketplace.Infrastructure.Bidding;

/// <summary>
/// V3 auto-bidding configuration. IsShadowMode gates the entire feature: when true, SEAP ingestion,
/// RAG and document generation run silently and are never shown to users and never charged via Stripe.
/// </summary>
public sealed class AutoBiddingOptions
{
    public const string SectionName = "AutoBidding";
    public bool IsShadowMode { get; init; }
    public string Model { get; init; } = "claude-sonnet-5";
    public string ApiKey { get; init; } = string.Empty;
    public string ApiUrl { get; init; } = "https://api.anthropic.com/v1/messages";
    public int MaxRequestTokens { get; init; } = 4000;
    public int TopChunks { get; init; } = 10;
    public string EmbeddingModel { get; init; } = "text-embedding-3-small";
    public string EmbeddingEndpoint { get; init; } = "https://api.openai.com/v1/embeddings";
    public string EmbeddingApiKey { get; init; } = string.Empty;
    public string DocumentsBucket { get; init; } = string.Empty;
    public string DocumentsRegion { get; init; } = "eu-central-1";
}
