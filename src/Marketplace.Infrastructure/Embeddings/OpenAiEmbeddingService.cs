using System.Net.Http.Json;
using System.Text.Json;
using Marketplace.Infrastructure.Bidding;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Marketplace.Infrastructure.Embeddings;

// OpenAI text-embedding-3-small via direct HTTP, reusing the existing OpenAI API key configuration.
public sealed partial class OpenAiEmbeddingService(
    IHttpClientFactory httpClientFactory,
    IOptions<AutoBiddingOptions> options,
    ILogger<OpenAiEmbeddingService> logger) : IEmbeddingService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.Value.EmbeddingApiKey))
            throw new InvalidOperationException("Cheia pentru embeddings OpenAI nu este configurată.");
        using HttpClient client = httpClientFactory.CreateClient("embeddings");
        client.Timeout = TimeSpan.FromSeconds(60);
        client.DefaultRequestHeaders.Authorization = new("Bearer", options.Value.EmbeddingApiKey);
        using HttpResponseMessage response = await client.PostAsJsonAsync(options.Value.EmbeddingEndpoint, new
        {
            model = options.Value.EmbeddingModel,
            input = text
        }, JsonOptions, cancellationToken);
        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            LogEmbeddingFailed(logger, (int)response.StatusCode);
            throw new HttpRequestException($"Embeddings returned {(int)response.StatusCode}.");
        }
        using JsonDocument document = JsonDocument.Parse(body);
        float[] embedding = document.RootElement.GetProperty("data")[0].GetProperty("embedding")
            .EnumerateArray().Select(value => value.GetSingle()).ToArray();
        return embedding;
    }

    [LoggerMessage(EventId = 5201, Level = LogLevel.Warning, Message = "OpenAI embeddings returned {StatusCode}.")]
    private static partial void LogEmbeddingFailed(ILogger logger, int statusCode);
}
