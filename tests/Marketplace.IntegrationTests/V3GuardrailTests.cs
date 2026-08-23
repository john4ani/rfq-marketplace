using System.Net;
using System.Text;
using System.Text.Json;
using Marketplace.Domain.Seap;
using Marketplace.Infrastructure.Bidding;
using Marketplace.Infrastructure.Resilience;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Marketplace.IntegrationTests;

// Guardrails for the V3 module: cost protection (token cap, consecutive-failure circuit breakers)
// and the SeapRawIngestion staging state machine.
public sealed class V3GuardrailTests
{
    [Fact]
    public void CircuitBreakerOpensAfterThreeConsecutiveFailuresAndHalfOpensAfterAnHour()
    {
        FakeTimeProvider clock = new();
        CircuitBreaker breaker = new(clock, failureThreshold: 3, openDuration: TimeSpan.FromHours(1));

        Assert.False(breaker.IsOpen);
        breaker.RecordFailure();
        breaker.RecordFailure();
        breaker.RecordFailure();
        Assert.True(breaker.IsOpen);

        clock.Now = clock.Now.AddMinutes(59);
        Assert.True(breaker.IsOpen);
        clock.Now = clock.Now.AddMinutes(2); // past the one-hour window
        Assert.False(breaker.IsOpen);
    }

    [Fact]
    public void CircuitBreakerSuccessResetsTheFailureCounter()
    {
        FakeTimeProvider clock = new();
        CircuitBreaker breaker = new(clock);

        breaker.RecordFailure();
        breaker.RecordFailure();
        breaker.RecordSuccess();
        breaker.RecordFailure();
        breaker.RecordFailure();
        Assert.False(breaker.IsOpen); // only two consecutive failures after the success
    }

    [Fact]
    public async Task ClaudeProviderHardCapsMaxTokensRegardlessOfConfiguration()
    {
        RecordingClaudeHandler handler = new("""{"content":[{"text":"raspuns"}]}""");
        StubHttpClientFactory factory = new(handler);
        IOptions<AutoBiddingOptions> options = Options.Create(new AutoBiddingOptions
        {
            ApiKey = "test-key",
            ApiUrl = "https://api.anthropic.com/v1/messages",
            MaxRequestTokens = 8000 // must be capped down to 4000 in code
        });

        AnthropicClaudeProvider provider = new(factory, options, NullLogger<AnthropicClaudeProvider>.Instance, new FakeTimeProvider());

        string result = await provider.CompleteAsync("sys", "user", CancellationToken.None);

        Assert.Equal("raspuns", result);
        using JsonDocument body = JsonDocument.Parse(handler.LastRequestBody!);
        Assert.Equal(4000, body.RootElement.GetProperty("max_tokens").GetInt32());
    }

    [Fact]
    public async Task ClaudeProviderStopsCallingAfterThreeConsecutiveFailures()
    {
        RecordingClaudeHandler handler = new(HttpStatusCode.InternalServerError);
        StubHttpClientFactory factory = new(handler);
        IOptions<AutoBiddingOptions> options = Options.Create(new AutoBiddingOptions { ApiKey = "test-key", MaxRequestTokens = 4000 });

        AnthropicClaudeProvider provider = new(factory, options, NullLogger<AnthropicClaudeProvider>.Instance, new FakeTimeProvider());

        for (int i = 0; i < 3; i++)
            await Assert.ThrowsAsync<HttpRequestException>(() => provider.CompleteAsync("sys", "user", CancellationToken.None));

        Assert.Equal(3, handler.CallCount);
        InvalidOperationException fourth = await Assert.ThrowsAsync<InvalidOperationException>(() => provider.CompleteAsync("sys", "user", CancellationToken.None));
        Assert.Contains("circuit", fourth.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(3, handler.CallCount); // no further HTTP calls while the circuit is open
    }

    [Fact]
    public void SeapRawIngestionTransitionsFromPendingToParsedOrFailed()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        SeapRawIngestion parsed = SeapRawIngestion.Pending("CN123", "{}", now);
        Assert.Equal(SeapRawIngestionStatus.Pending, parsed.Status);
        parsed.MarkParsed(now);
        Assert.Equal(SeapRawIngestionStatus.Parsed, parsed.Status);

        SeapRawIngestion failed = SeapRawIngestion.Pending("CN456", "{}", now);
        failed.MarkFailed("Titlul anunțului este obligatoriu.", now);
        Assert.Equal(SeapRawIngestionStatus.Failed, failed.Status);
        Assert.Equal("Titlul anunțului este obligatoriu.", failed.ErrorMessage);
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class RecordingClaudeHandler : HttpMessageHandler
    {
        private readonly string? jsonBody;
        private readonly HttpStatusCode status;

        public int CallCount { get; private set; }
        public string? LastRequestBody { get; private set; }

        public RecordingClaudeHandler(string jsonBody)
        {
            this.jsonBody = jsonBody;
            status = HttpStatusCode.OK;
        }

        public RecordingClaudeHandler(HttpStatusCode status) => this.status = status;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            LastRequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            if (status != HttpStatusCode.OK)
                return new HttpResponseMessage(status) { RequestMessage = request };
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(jsonBody!, Encoding.UTF8, "application/json"),
                RequestMessage = request
            };
        }
    }

    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }
}
