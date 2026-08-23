namespace Marketplace.Infrastructure.Bidding;

/// <summary>
/// LLM provider abstraction (per the testing addendum: standard tests use StaticMockLlmProvider,
/// never a real Anthropic call).
/// </summary>
public interface ILlmProvider
{
    Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken);
}
