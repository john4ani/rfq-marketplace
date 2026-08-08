using System.Net;
using System.Text;
using System.Text.Json;
using Marketplace.Infrastructure.AiIntake;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Marketplace.IntegrationTests;

public sealed class OpenAiNeedExtractionProviderTests
{
    [Fact]
    public async Task UsesDirectOpenAiWithoutWebSearch()
    {
        var handler = new RecordingHandler(ResponseBody());
        using var client = new HttpClient(handler);
        var logger = new RecordingLogger();
        var settings = Options.Create(new AiIntakeOptions
        {
            Provider = AiIntakeProvider.OpenAi,
            ApiKey = "openai-test-key",
            Model = "gpt-5.6-luna"
        });
        var provider = new OpenAiNeedExtractionProvider(client, settings, logger);

        NeedExtractionOutcome outcome = await provider.ExtractAsync(Context(), CancellationToken.None);

        Assert.True(provider.IsAvailable);
        Assert.Equal("OpenAI", outcome.Provider);
        Assert.Empty(outcome.ResearchSources);
        Assert.Equal("Bearer", handler.AuthorizationScheme);
        Assert.Equal("openai-test-key", handler.AuthorizationParameter);
        using JsonDocument request = JsonDocument.Parse(handler.RequestBody!);
        Assert.Equal("gpt-5.6-luna", request.RootElement.GetProperty("model").GetString());
        Assert.Equal("none", request.RootElement.GetProperty("reasoning").GetProperty("effort").GetString());
        Assert.Equal("low", request.RootElement.GetProperty("text").GetProperty("verbosity").GetString());
        Assert.False(request.RootElement.TryGetProperty("tools", out _));
        Assert.Contains("\"Kilogram\"", handler.RequestBody, StringComparison.Ordinal);
        Assert.Contains("Nu ai acces la web search", handler.RequestBody, StringComparison.Ordinal);
        LoggedEntry timing = Assert.Single(logger.Entries);
        Assert.Equal(4201, timing.EventId.Id);
        Assert.Equal(LogLevel.Information, timing.Level);
        Assert.Contains("gpt-5.6-luna", timing.Message, StringComparison.Ordinal);
        Assert.Contains("api.openai.com", timing.Message, StringComparison.Ordinal);
        Assert.Contains("input tokens: 120", timing.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(Context().InitialInput, timing.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UsesAzureApiKeyHeaderWhenAzureIsSelected()
    {
        var handler = new RecordingHandler(ResponseBody());
        using var client = new HttpClient(handler);
        var provider = new OpenAiNeedExtractionProvider(client, Options.Create(new AiIntakeOptions
        {
            Provider = AiIntakeProvider.AzureOpenAi,
            ApiKey = "azure-test-key",
            Endpoint = "https://example.openai.azure.com/openai/v1/responses",
            Model = "gpt-5.6-luna"
        }), NullLogger<OpenAiNeedExtractionProvider>.Instance);

        NeedExtractionOutcome outcome = await provider.ExtractAsync(Context(), CancellationToken.None);

        Assert.Equal("Azure OpenAI", outcome.Provider);
        Assert.Null(handler.AuthorizationScheme);
        Assert.Equal("azure-test-key", Assert.Single(handler.ApiKeyValues));
    }

    [Theory]
    [InlineData("", "https://api.openai.com/v1/responses")]
    [InlineData("configured", "not-an-endpoint")]
    public void IsUnavailableWithoutCompleteConfiguration(string apiKey, string endpoint)
    {
        var provider = new OpenAiNeedExtractionProvider(new HttpClient(new RecordingHandler("{}")),
            Options.Create(new AiIntakeOptions { ApiKey = apiKey, Endpoint = endpoint }),
            NullLogger<OpenAiNeedExtractionProvider>.Instance);

        Assert.False(provider.IsAvailable);
    }

    private static NeedExtractionContext Context() => new(
        Guid.NewGuid(), "Am nevoie de trei usi din lemn.", "Iasi", "Iasi", 50,
        [new IntakeCategory(Guid.NewGuid(), "Materiale", "materiale")], [], null,
        new DateTimeOffset(2026, 8, 4, 12, 0, 0, TimeSpan.Zero));

    private static string ResponseBody()
    {
        string extraction = JsonSerializer.Serialize(new
        {
            inputType = "ProjectDescription", title = "Trei usi din lemn", projectName = (string?)null,
            description = "Necesar propus dupa clarificare si research.", primaryCategorySlug = "materiale",
            deliveryAddress = "", deliveryLocality = "Iasi", county = "Iasi",
            preferredDeliveryDate = (string?)null, quotationDeadline = "2026-08-08 12:00",
            alternativeProductsAccepted = true, allowSplit = false, deliveryRequired = true,
            estimatedBudget = (decimal?)null, buyerNotes = (string?)null,
            lines = new[] { new { kind = "Product", name = "Usa din lemn", specification = "Dimensiuni de confirmat",
                quantity = 3, unit = "Piece", alternativesAccepted = true, categorySlug = "materiale" } },
            assumptions = Array.Empty<string>(), uncertainties = new List<string> { "Feroneria se confirma cu furnizorul." },
            researchSources = Array.Empty<object>(),
            clarification = (object?)null
        });
        return JsonSerializer.Serialize(new
        {
            output = new object[]
            {
                new { type = "message", content = new[] { new { type = "output_text", text = extraction,
                    annotations = Array.Empty<object>() } } }
            },
            usage = new { input_tokens = 120, output_tokens = 80 }
        });
    }

    private sealed class RecordingHandler(string responseBody) : HttpMessageHandler
    {
        public string? RequestBody { get; private set; }
        public string? AuthorizationScheme { get; private set; }
        public string? AuthorizationParameter { get; private set; }
        public IReadOnlyCollection<string> ApiKeyValues { get; private set; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            AuthorizationParameter = request.Headers.Authorization?.Parameter;
            ApiKeyValues = request.Headers.TryGetValues("api-key", out IEnumerable<string>? values)
                ? values.ToArray()
                : [];
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class RecordingLogger : ILogger<OpenAiNeedExtractionProvider>
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
