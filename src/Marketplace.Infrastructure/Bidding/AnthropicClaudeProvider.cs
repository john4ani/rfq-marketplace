using System.Net.Http.Json;
using System.Text.Json;
using Marketplace.Application.AutoBidding;
using Marketplace.Infrastructure.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
namespace Marketplace.Infrastructure.Bidding;

// Anthropic Claude Messages API via direct HTTP (IHttpClientFactory). Code-complete; requires an API
// key to activate. Standard tests never call this provider (they use StaticMockLlmProvider).
// Cost guardrails: max_tokens is hard-capped at MaxTokensCap regardless of configuration, and three
// consecutive failures open a one-hour circuit breaker so a misbehaving API cannot burn spend.
public sealed partial class AnthropicClaudeProvider : ILlmProvider
{
    private const int MaxTokensCap = 16000;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IHttpClientFactory httpClientFactory;
    private readonly AutoBiddingOptions settings;
    private readonly ILogger<AnthropicClaudeProvider> logger;
    private readonly CircuitBreaker circuitBreaker;

    public AnthropicClaudeProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<AutoBiddingOptions> options,
        ILogger<AnthropicClaudeProvider> logger,
        TimeProvider timeProvider)
    {
        this.httpClientFactory = httpClientFactory;
        settings = options.Value;
        this.logger = logger;
        circuitBreaker = new CircuitBreaker(timeProvider);
    }

    public async Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
            throw new AutoBiddingPipelineException(503, "Cheia API Anthropic nu este configurată în acest mediu.");
        if (circuitBreaker.IsOpen)
            throw new AutoBiddingPipelineException(503, "Serviciul Claude este temporar indisponibil (circuit deschis). Reîncearcă peste aproximativ o oră.");

        try
        {
            using HttpClient client = httpClientFactory.CreateClient("claude");
            client.Timeout = TimeSpan.FromSeconds(120);
            client.DefaultRequestHeaders.Add("x-api-key", settings.ApiKey);
            client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
            using HttpResponseMessage response = await client.PostAsJsonAsync(settings.ApiUrl, new
            {
                model = settings.Model,
                max_tokens = Math.Min(Math.Max(256, settings.MaxRequestTokens), MaxTokensCap),
                // The proposal must be a direct JSON answer. claude-sonnet-5 emits a "thinking" content block
                // by default (breaking the content[0].text assumption); disable extended thinking so the first
                // block is the text.
                thinking = new { type = "disabled" },
                system = systemPrompt,
                messages = new[] { new { role = "user", content = userPrompt } }
            }, JsonOptions, cancellationToken);
            string body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                LogClaudeFailed(logger, (int)response.StatusCode);
                throw new AutoBiddingPipelineException(503,
                    $"Serviciul Claude a răspuns cu eroarea {(int)response.StatusCode}. Reîncearcă generarea în câteva minute.");
            }
            circuitBreaker.RecordSuccess();
            return ClaudeResponse.ExtractText(body);
        }
        catch (AutoBiddingPipelineException)
        {
            // Already carries a user-facing message; only record the hit on the cost circuit breaker.
            circuitBreaker.RecordFailure();
            throw;
        }
        catch (Exception exception)
        {
            // Network failures, timeouts and unreadable responses are service-level problems: record the
            // circuit-breaker hit and surface one actionable message instead of a bare 500 (the middleware
            // maps AutoBiddingPipelineException to ProblemDetails with a friendly Romanian detail).
            circuitBreaker.RecordFailure();
            throw new AutoBiddingPipelineException(503,
                "Serviciul Claude este temporar indisponibil sau răspunsul său nu a putut fi citit. Reîncearcă generarea în câteva minute.", exception);
        }
    }

    [LoggerMessage(EventId = 5301, Level = LogLevel.Warning, Message = "Anthropic Claude returned {StatusCode}.")]
    private static partial void LogClaudeFailed(ILogger logger, int statusCode);
}
