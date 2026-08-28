using System.Net;
using System.Text;
using System.Text.Json;
using Marketplace.Application.AiIntake;
using Marketplace.Application.Seap;
using Marketplace.Infrastructure.AiIntake;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Marketplace.IntegrationTests;

public sealed class OpenAiSeapTenderMatchInterpreterTests
{
    [Fact]
    public async Task TranslatesDescriptionToRigidFiltersThroughDirectOpenAi()
    {
        var handler = new RecordingHandler(MatchResponseBody());
        using var client = new HttpClient(handler);
        var logger = new RecordingLogger();
        var settings = Options.Create(new AiIntakeOptions
        {
            Provider = AiIntakeProvider.OpenAi,
            ApiKey = "openai-test-key",
            Model = "gpt-5.6-luna"
        });
        var interpreter = new OpenAiSeapTenderMatchInterpreter(new OpenAiResponsesTransport(client, settings, logger));

        SeapTenderMatchResult result = await interpreter.InterpretAsync(Request(), CancellationToken.None);

        Assert.True(interpreter.IsAvailable);
        Assert.False(result.ClarificationNeeded);
        Assert.Null(result.ClarificationQuestion);
        Assert.Equal(["45233000", "45221111"], result.ExtractedCpvPrefixes);
        Assert.Equal(["IS", "SV", "BT"], result.ExtractedCountyCodes);
        // County codes are normalized deterministically to the canonical names the database filters by.
        Assert.Equal(["Iași", "Suceava", "Botoșani"], result.ExtractedCounties);
        Assert.Equal("Am identificat interes pentru Lucrări de drumuri și poduri în zona Moldovei.", result.SummaryText);
        Assert.Equal("OpenAI", result.Provider);
        Assert.Equal("Bearer", handler.AuthorizationScheme);
        Assert.Equal("openai-test-key", handler.AuthorizationParameter);
        using JsonDocument request = JsonDocument.Parse(handler.RequestBody!);
        Assert.Equal("gpt-5.6-luna", request.RootElement.GetProperty("model").GetString());
        Assert.Equal("none", request.RootElement.GetProperty("reasoning").GetProperty("effort").GetString());
        Assert.Equal("seap_tender_match", request.RootElement.GetProperty("text").GetProperty("format").GetProperty("name").GetString());
        Assert.False(request.RootElement.TryGetProperty("tools", out _));
        Assert.Equal(HttpVersion.Version20, handler.RequestVersion);
        Assert.Contains("codurile de jude", handler.RequestBody, StringComparison.Ordinal);
        Assert.Contains("45233000", handler.RequestBody, StringComparison.Ordinal);
        Assert.DoesNotContain("web search", handler.RequestBody, StringComparison.OrdinalIgnoreCase);
        LoggedEntry timing = Assert.Single(logger.Entries);
        Assert.Equal(4201, timing.EventId.Id);
        Assert.Equal(LogLevel.Information, timing.Level);
        Assert.Contains("gpt-5.6-luna", timing.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(Request().InitialInput, timing.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AsksExactlyOneClarificationWhenDescriptionIsTooVague()
    {
        var handler = new RecordingHandler(ClarificationResponseBody());
        using var client = new HttpClient(handler);
        var interpreter = new OpenAiSeapTenderMatchInterpreter(new OpenAiResponsesTransport(client,
            Options.Create(new AiIntakeOptions { Provider = AiIntakeProvider.OpenAi, ApiKey = "openai-test-key", Model = "gpt-5.6-luna" }),
            NullLogger<OpenAiResponsesTransport>.Instance));

        SeapTenderMatchResult result = await interpreter.InterpretAsync(Request(), CancellationToken.None);

        Assert.True(result.ClarificationNeeded);
        Assert.Equal("În ce domeniu cauti anunțuri?", result.ClarificationQuestion);
        Assert.Empty(result.ExtractedCpvPrefixes);
        Assert.Empty(result.ExtractedCountyCodes);
        Assert.Empty(result.ExtractedCounties);
    }

    [Fact]
    public async Task UsesAzureApiKeyHeaderWhenAzureIsSelected()
    {
        var handler = new RecordingHandler(MatchResponseBody());
        using var client = new HttpClient(handler);
        var interpreter = new OpenAiSeapTenderMatchInterpreter(new OpenAiResponsesTransport(client,
            Options.Create(new AiIntakeOptions
            {
                Provider = AiIntakeProvider.AzureOpenAi,
                ApiKey = "azure-test-key",
                Endpoint = "https://example.openai.azure.com/openai/v1/responses",
                Model = "gpt-5.6-luna"
            }),
            NullLogger<OpenAiResponsesTransport>.Instance));

        await interpreter.InterpretAsync(Request(), CancellationToken.None);

        Assert.Null(handler.AuthorizationHeader);
        Assert.Equal("azure-test-key", Assert.Single(handler.ApiKeyValues));
    }

    private static SeapTenderMatchRequest Request() =>
        new("Căutăm licitații pentru lucrări de drumuri și poduri în zona Moldovei.", []);

    private static readonly string[] ExtractedCpvPrefixes = ["45233000", "45221111"];
    private static readonly string[] ExtractedCountyCodes = ["IS", "SV", "BT"];

    private static string MatchResponseBody()
    {
        string extraction = JsonSerializer.Serialize(new
        {
            clarificationNeeded = false,
            clarificationQuestion = (string?)null,
            extractedCpvPrefixes = ExtractedCpvPrefixes,
            extractedCountyCodes = ExtractedCountyCodes,
            summaryText = "Am identificat interes pentru Lucrări de drumuri și poduri în zona Moldovei."
        });
        return Envelope(extraction);
    }

    private static string ClarificationResponseBody()
    {
        string extraction = JsonSerializer.Serialize(new
        {
            clarificationNeeded = true,
            clarificationQuestion = "În ce domeniu cauti anunțuri?",
            extractedCpvPrefixes = Array.Empty<string>(),
            extractedCountyCodes = Array.Empty<string>(),
            summaryText = (string?)null
        });
        return Envelope(extraction);
    }

    private static string Envelope(string extraction) => JsonSerializer.Serialize(new
    {
        output = new object[]
        {
            new { type = "message", content = new[] { new { type = "output_text", text = extraction,
                annotations = Array.Empty<object>() } } }
        },
        usage = new { input_tokens = 120, output_tokens = 80 }
    });

    private sealed class RecordingHandler(string responseBody) : HttpMessageHandler
    {
        public string? RequestBody { get; private set; }
        public string? AuthorizationScheme { get; private set; }
        public string? AuthorizationParameter { get; private set; }
        public string? AuthorizationHeader { get; private set; }
        public IReadOnlyCollection<string> ApiKeyValues { get; private set; } = [];
        public Version? RequestVersion { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            RequestVersion = request.Version;
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            AuthorizationParameter = request.Headers.Authorization?.Parameter;
            AuthorizationHeader = request.Headers.Authorization?.ToString();
            ApiKeyValues = request.Headers.TryGetValues("api-key", out IEnumerable<string>? values)
                ? values.ToArray()
                : [];
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class RecordingLogger : ILogger<OpenAiResponsesTransport>
    {
        public List<LoggedEntry> Entries { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NoopScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Entries.Add(new(logLevel, eventId, formatter(state, exception)));
    }

    private sealed record LoggedEntry(LogLevel Level, EventId EventId, string Message);

    private sealed class NoopScope : IDisposable
    {
        public static readonly NoopScope Instance = new();
        public void Dispose() { }
    }
}

