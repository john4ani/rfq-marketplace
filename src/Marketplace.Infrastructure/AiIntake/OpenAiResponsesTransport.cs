using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Marketplace.Infrastructure.AiIntake;

// Single OpenAI Responses transport shared by every interpreter. General and Construction differ only in their
// prompt, JSON schema and mapping; the HTTP client, Responses API call, telemetry and configuration live here once
// (ADR-002 / Task 10 §14 — do not duplicate transport just because prompts differ).
public sealed partial class OpenAiResponsesTransport(
    HttpClient httpClient,
    IOptions<AiIntakeOptions> options,
    ILogger<OpenAiResponsesTransport> logger)
{
    private readonly AiIntakeOptions settings = options.Value;
    private static readonly string[] WebSearchSourcesInclude = ["web_search_call.action.sources"];

    public bool IsAvailable => settings.Enabled
        && !string.IsNullOrWhiteSpace(settings.ApiKey)
        && !string.IsNullOrWhiteSpace(settings.Model)
        && Uri.TryCreate(settings.Endpoint, UriKind.Absolute, out _);

    public string ProviderName => settings.Provider == AiIntakeProvider.AzureOpenAi ? "Azure OpenAI" : "OpenAI";
    public string Model => settings.Model;
    public string PromptVersion => settings.PromptVersion;

    public async Task<OpenAiStructuredResponse> SendAsync(OpenAiStructuredRequest request, CancellationToken cancellationToken)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        int? statusCode = null;
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, settings.Endpoint);
        httpRequest.Version = HttpVersion.Version20;
        httpRequest.VersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;
        if (settings.Provider == AiIntakeProvider.AzureOpenAi)
            httpRequest.Headers.Add("api-key", settings.ApiKey);
        else
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
        httpRequest.Content = JsonContent.Create(BuildPayload(request));
        try
        {
            using HttpResponseMessage response = await httpClient.SendAsync(
                httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            statusCode = (int)response.StatusCode;
            string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"AI intake provider returned {statusCode}.");

            using JsonDocument document = JsonDocument.Parse(responseBody);
            string outputText = document.RootElement.GetProperty("output").EnumerateArray()
                .SelectMany(item => item.TryGetProperty("content", out JsonElement content) ? content.EnumerateArray() : [])
                .Where(item => item.TryGetProperty("type", out JsonElement type) && type.GetString() == "output_text")
                .Select(item => item.GetProperty("text").GetString()).FirstOrDefault()
                ?? throw new InvalidOperationException("AI intake provider did not return structured output.");
            int inputTokens = ReadUsage(document.RootElement, "input_tokens");
            int outputTokens = ReadUsage(document.RootElement, "output_tokens");
            if (logger.IsEnabled(LogLevel.Information))
            {
                string providerName = ProviderName;
                string endpointHost = EndpointHost();
                long durationMs = stopwatch.ElapsedMilliseconds;
                LogProviderCompleted(logger, providerName, settings.Model, endpointHost, statusCode.Value,
                    durationMs, inputTokens, outputTokens);
            }
            return new OpenAiStructuredResponse(outputText, inputTokens, outputTokens);
        }
        catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
        {
            if (logger.IsEnabled(LogLevel.Warning))
            {
                string providerName = ProviderName;
                string endpointHost = EndpointHost();
                long durationMs = stopwatch.ElapsedMilliseconds;
                LogProviderCancelled(logger, providerName, settings.Model, endpointHost, statusCode, durationMs, exception);
            }
            throw;
        }
        catch (Exception exception)
        {
            if (logger.IsEnabled(LogLevel.Warning))
            {
                string providerName = ProviderName;
                string endpointHost = EndpointHost();
                long durationMs = stopwatch.ElapsedMilliseconds;
                LogProviderFailed(logger, providerName, settings.Model, endpointHost, statusCode, durationMs, exception);
            }
            throw;
        }
    }

    public async Task<OpenAiWebDiscoveryResponse> SendWebDiscoveryAsync(OpenAiWebDiscoveryRequest request, CancellationToken cancellationToken)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        int? statusCode = null;
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, settings.Endpoint);
        httpRequest.Version = HttpVersion.Version20;
        httpRequest.VersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;
        if (settings.Provider == AiIntakeProvider.AzureOpenAi) httpRequest.Headers.Add("api-key", settings.ApiKey);
        else httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
        httpRequest.Content = JsonContent.Create(new
        {
            model = settings.Model,
            store = false,
            reasoning = new { effort = "none" },
            input = request.Instructions,
            tools = new[] { new { type = "web_search", search_context_size = "low", user_location = new { type = "approximate", country = "RO", region = request.County, city = request.Locality } } },
            max_tool_calls = 2,
            include = WebSearchSourcesInclude,
            text = new { verbosity = "low", format = new { type = "json_schema", name = request.SchemaName, strict = true, schema = request.Schema } },
            max_output_tokens = request.MaxOutputTokens
        });
        try
        {
            using HttpResponseMessage response = await httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            statusCode = (int)response.StatusCode;
            string body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"AI intake provider returned {statusCode}.", null, response.StatusCode);
            using JsonDocument document = JsonDocument.Parse(body);
            JsonElement root = document.RootElement;
            string? outputText = ReadOutputText(root);
            string status = root.TryGetProperty("status", out JsonElement statusElement) ? statusElement.GetString() ?? "failed" : "failed";
            int calls = root.GetProperty("output").EnumerateArray().Count(item => item.TryGetProperty("type", out JsonElement type) && type.GetString() == "web_search_call");
            string sourcesJson = ReadSources(root);
            int inputTokens = ReadUsage(root, "input_tokens"); int outputTokens = ReadUsage(root, "output_tokens");
            if (!string.Equals(status, "completed", StringComparison.Ordinal) || string.IsNullOrWhiteSpace(outputText))
                throw new InvalidOperationException("Web discovery did not return structured evidence.");
            if (logger.IsEnabled(LogLevel.Information))
            {
                string providerName = ProviderName;
                string endpointHost = EndpointHost();
                long durationMs = stopwatch.ElapsedMilliseconds;
                LogProviderCompleted(logger, providerName, settings.Model, endpointHost, statusCode.Value, durationMs, inputTokens, outputTokens);
            }
            return new(outputText, sourcesJson, calls, stopwatch.ElapsedMilliseconds, inputTokens, outputTokens);
        }
        catch (Exception exception)
        {
            LogProviderFailed(logger, ProviderName, settings.Model, EndpointHost(), statusCode, stopwatch.ElapsedMilliseconds, exception);
            throw;
        }
    }

    private object BuildPayload(OpenAiStructuredRequest request) => new
    {
        model = settings.Model,
        store = false,
        reasoning = new { effort = settings.ReasoningEffort },
        safety_identifier = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.SafetySeed.ToString()))).ToLowerInvariant(),
        input = new[] { new { role = "system", content = request.Instructions }, new { role = "user", content = request.UserInput } },
        text = new { verbosity = settings.TextVerbosity, format = new { type = "json_schema", name = request.SchemaName, strict = true, schema = request.Schema } },
        max_output_tokens = request.MaxOutputTokens
    };

    private string EndpointHost() => Uri.TryCreate(settings.Endpoint, UriKind.Absolute, out Uri? endpoint)
        ? endpoint.Host
        : "invalid-endpoint";

    private static int ReadUsage(JsonElement root, string property) =>
        root.TryGetProperty("usage", out JsonElement usage) && usage.TryGetProperty(property, out JsonElement value)
            ? value.GetInt32() : 0;

    private static string? ReadOutputText(JsonElement root) => root.GetProperty("output").EnumerateArray()
        .Where(item => item.TryGetProperty("type", out JsonElement type) && type.GetString() == "message")
        .SelectMany(item => item.TryGetProperty("content", out JsonElement content) ? content.EnumerateArray() : [])
        .Where(item => item.TryGetProperty("type", out JsonElement type) && type.GetString() == "output_text")
        .Select(item => item.TryGetProperty("text", out JsonElement text) ? text.GetString() : null).FirstOrDefault();

    private static string ReadSources(JsonElement root)
    {
        var sources = root.GetProperty("output").EnumerateArray()
            .Where(item => item.TryGetProperty("type", out JsonElement type) && type.GetString() == "web_search_call")
            .SelectMany(item => item.TryGetProperty("action", out JsonElement action) && action.TryGetProperty("sources", out JsonElement values) ? values.EnumerateArray() : [])
            .Select(item => new { url = item.TryGetProperty("url", out JsonElement url) ? url.GetString() : null, title = item.TryGetProperty("title", out JsonElement title) ? title.GetString() : null })
            .Where(item => !string.IsNullOrWhiteSpace(item.url)).Distinct().ToArray();
        return JsonSerializer.Serialize(sources);
    }

    [LoggerMessage(EventId = 4201, Level = LogLevel.Information,
        Message = "AI intake provider call completed. Provider: {Provider}; model: {Model}; endpoint host: {EndpointHost}; status: {StatusCode}; duration: {DurationMs}ms; input tokens: {InputTokens}; output tokens: {OutputTokens}.")]
    private static partial void LogProviderCompleted(ILogger logger, string provider, string model, string endpointHost,
        int statusCode, long durationMs, int inputTokens, int outputTokens);

    [LoggerMessage(EventId = 4202, Level = LogLevel.Warning,
        Message = "AI intake provider call was cancelled. Provider: {Provider}; model: {Model}; endpoint host: {EndpointHost}; status: {StatusCode}; duration: {DurationMs}ms.")]
    private static partial void LogProviderCancelled(ILogger logger, string provider, string model, string endpointHost,
        int? statusCode, long durationMs, Exception exception);

    [LoggerMessage(EventId = 4203, Level = LogLevel.Warning,
        Message = "AI intake provider call failed. Provider: {Provider}; model: {Model}; endpoint host: {EndpointHost}; status: {StatusCode}; duration: {DurationMs}ms.")]
    private static partial void LogProviderFailed(ILogger logger, string provider, string model, string endpointHost,
        int? statusCode, long durationMs, Exception exception);
}

public sealed record OpenAiStructuredRequest(
    string Instructions, string UserInput, string SchemaName, JsonNode Schema, Guid SafetySeed, int MaxOutputTokens = 2500);

public sealed record OpenAiStructuredResponse(string OutputText, int InputTokens, int OutputTokens);
public sealed record OpenAiWebDiscoveryRequest(string Instructions, string SchemaName, JsonNode Schema, string Locality, string County, int MaxOutputTokens = 1800);
public sealed record OpenAiWebDiscoveryResponse(string OutputText, string SourcesJson, int WebSearchCallCount, long LatencyMs, int InputTokens, int OutputTokens);
