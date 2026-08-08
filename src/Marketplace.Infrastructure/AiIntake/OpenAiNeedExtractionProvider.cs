using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Marketplace.Application.AiIntake;
using Marketplace.Domain.Rfqs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Marketplace.Infrastructure.AiIntake;

internal sealed partial class OpenAiNeedExtractionProvider(
    HttpClient httpClient,
    IOptions<AiIntakeOptions> options,
    ILogger<OpenAiNeedExtractionProvider> logger) : INeedExtractionProvider
{
    private readonly AiIntakeOptions settings = options.Value;
    public bool IsAvailable => settings.Enabled
        && !string.IsNullOrWhiteSpace(settings.ApiKey)
        && !string.IsNullOrWhiteSpace(settings.Model)
        && Uri.TryCreate(settings.Endpoint, UriKind.Absolute, out _);

    public async Task<NeedExtractionOutcome> ExtractAsync(NeedExtractionContext context, CancellationToken cancellationToken)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        int? statusCode = null;
        using var request = new HttpRequestMessage(HttpMethod.Post, settings.Endpoint);
        request.Version = HttpVersion.Version20;
        request.VersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;
        if (settings.Provider == AiIntakeProvider.AzureOpenAi)
            request.Headers.Add("api-key", settings.ApiKey);
        else
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
        request.Content = JsonContent.Create(BuildRequest(context));
        try
        {
            using HttpResponseMessage response = await httpClient.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
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
            AiExtraction extraction = JsonSerializer.Deserialize<AiExtraction>(outputText, JsonOptions)
                ?? throw new InvalidOperationException("AI intake structured output is empty.");
            NeedExtractionOutcome outcome = Map(context, extraction, document.RootElement);
            if (logger.IsEnabled(LogLevel.Information))
            {
                string providerName = ProviderName();
                string endpointHost = EndpointHost();
                long durationMs = stopwatch.ElapsedMilliseconds;
                LogProviderCompleted(logger, providerName, settings.Model, endpointHost, statusCode.Value,
                    durationMs, outcome.InputTokens, outcome.OutputTokens);
            }
            return outcome;
        }
        catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
        {
            if (logger.IsEnabled(LogLevel.Warning))
            {
                string providerName = ProviderName();
                string endpointHost = EndpointHost();
                long durationMs = stopwatch.ElapsedMilliseconds;
                LogProviderCancelled(logger, providerName, settings.Model, endpointHost, statusCode,
                    durationMs, exception);
            }
            throw;
        }
        catch (Exception exception)
        {
            if (logger.IsEnabled(LogLevel.Warning))
            {
                string providerName = ProviderName();
                string endpointHost = EndpointHost();
                long durationMs = stopwatch.ElapsedMilliseconds;
                LogProviderFailed(logger, providerName, settings.Model, endpointHost, statusCode,
                    durationMs, exception);
            }
            throw;
        }
    }

    private object BuildRequest(NeedExtractionContext context)
    {
        string categories = string.Join("\n", context.Categories.Select(item => $"- {item.Slug}: {item.Name}"));
        string turns = context.Turns.Count == 0 ? "Nicio clarificare încă." : string.Join("\n",
            context.Turns.Select(turn => $"Î: {turn.Question}\nR: {turn.Answer}"));
        string instructions = """
            Ești asistentul de achiziții CompaSpot. Fluxul obligatoriu este: clarificare, apoi research, apoi propunere
            de necesar pentru confirmare umană. Nu publica și nu prezenta propunerea drept specificație tehnică finală.

            1. Clarificare: identifică informația cu cel mai mare impact asupra necesarului și pune exact o întrebare
            scurtă pe rând. Pentru elemente construite la comandă (de exemplu uși sau porți), clarifică numărul și
            gruparea, dimensiunile fiecărui tip distinct, utilizarea și dacă utilizatorul dorește produs finit/serviciu
            sau numai materiale. Cât timp lipsește o informație esențială, clarification nu poate fi null.
            2. Analiză: numai după ce există suficiente date comerciale, folosește cunoștințele generale ale modelului
            pentru a identifica componentele uzuale și întrebările relevante. Nu ai acces la web search. Nu inventa
            surse, URL-uri, cantități, mărci sau standarde; orice detaliu incert trebuie marcat pentru verificare.
            3. Propunere: creează o listă editabilă de produse și/sau servicii. Pentru o lucrare completă păstrează
            serviciul ca opțiune și propune materiale separate doar dacă utilizatorul le-a cerut. Marchează explicit
            presupunerile și aspectele de verificat.
            4. Confirmare: când propunerea poate fi verificată de utilizator, setează clarification la null. Niciodată
            nu considera cererea publicată sau aprobată.

            categorySlug trebuie să fie null sau unul dintre slug-urile furnizate. Scrie toate textele în română.
            Pentru fiecare poziție cu cantitate, unit trebuie să fie una dintre valorile permise de schemă; nu lăsa unit null.
            Pentru servicii discrete fără altă unitate potrivită, folosește Piece.
            """;
        string input = $"""
            Descriere inițială:
            {context.InitialInput}

            Moment curent UTC: {context.Now:O}
            Profil livrare implicit: {context.BuyerCity}, {context.BuyerCounty}; rază {context.BuyerRadiusKm} km.
            Categorii valide:
            {categories}

            Clarificări anterioare:
            {turns}
            """;
        return new
        {
            model = settings.Model,
            store = false,
            reasoning = new { effort = settings.ReasoningEffort },
            safety_identifier = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(context.BuyerId.ToString()))).ToLowerInvariant(),
            input = new[] { new { role = "system", content = instructions }, new { role = "user", content = input } },
            text = new { verbosity = settings.TextVerbosity, format = new { type = "json_schema", name = "procurement_need", strict = true, schema = Schema } },
            max_output_tokens = 2500
        };
    }

    private NeedExtractionOutcome Map(NeedExtractionContext context, AiExtraction extraction, JsonElement root)
    {
        Dictionary<string, IntakeCategory> categories = context.Categories.ToDictionary(item => item.Slug, StringComparer.OrdinalIgnoreCase);
        IntakeCategory? primary = Resolve(extraction.PrimaryCategorySlug, categories);
        AssistedRequirementLine[] lines = extraction.Lines.Take(25).Select(line => new AssistedRequirementLine(
            Enum.TryParse(line.Kind, true, out RequirementKind kind) ? kind : RequirementKind.Product,
            Clean(line.Name, 160, "Poziție de clarificat"), CleanNullable(line.Specification, 1000),
            line.Quantity > 0 ? line.Quantity : null,
            Enum.TryParse(line.Unit, true, out UnitOfMeasure unit) ? unit : null,
            line.AlternativesAccepted, Resolve(line.CategorySlug, categories)?.Id ?? primary?.Id)).ToArray();
        if (lines.Length == 0)
            lines = [new(RequirementKind.Product, "Poziție de clarificat", null, null, null, true, primary?.Id)];

        AssistedClarification? clarification = extraction.Clarification is null ? null : new(
            Enum.TryParse(extraction.Clarification.Field, true, out ClarificationField field) ? field : ClarificationField.Specification,
            Clean(extraction.Clarification.Question, 300, "Ce detaliu comercial mai trebuie precizat?"),
            extraction.Clarification.Choices.Where(item => !string.IsNullOrWhiteSpace(item)).Take(8).ToArray());
        ProcurementInputType inputType = Enum.TryParse(extraction.InputType, true, out ProcurementInputType parsed)
            ? parsed : ProcurementInputType.FreeText;
        DateTimeOffset quotationDeadline = ParseQuotationDeadline(extraction.QuotationDeadline, context.Now);
        var draft = new AssistedNeedDraft(inputType, Clean(extraction.Title, 180, "Cerere nouă"),
            CleanNullable(extraction.ProjectName, 180), CleanNullable(extraction.Description, 4000), primary?.Id,
            Clean(extraction.DeliveryAddress, 300, string.Empty),
            Clean(extraction.DeliveryLocality, 120, context.BuyerCity), Clean(extraction.County, 80, context.BuyerCounty),
            context.BuyerRadiusKm, ParsePreferredDeliveryDate(extraction.PreferredDeliveryDate), quotationDeadline,
            extraction.AlternativeProductsAccepted, extraction.AllowSplit, extraction.DeliveryRequired,
            extraction.EstimatedBudget is > 0 ? extraction.EstimatedBudget : null,
            CleanNullable(extraction.BuyerNotes, 1000), lines);
        int inputTokens = ReadUsage(root, "input_tokens");
        int outputTokens = ReadUsage(root, "output_tokens");
        return new(AssistedIntakeMode.OpenAi, ProviderName(), settings.Model, settings.PromptVersion, draft,
            extraction.Assumptions.Where(NotBlank).Take(20).ToArray(),
            extraction.Uncertainties.Where(NotBlank).Take(20).ToArray(), [], clarification, inputTokens, outputTokens);
    }

    private string ProviderName() => settings.Provider == AiIntakeProvider.AzureOpenAi
        ? "Azure OpenAI"
        : "OpenAI";

    private string EndpointHost() => Uri.TryCreate(settings.Endpoint, UriKind.Absolute, out Uri? endpoint)
        ? endpoint.Host
        : "invalid-endpoint";

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

    private static int ReadUsage(JsonElement root, string property) =>
        root.TryGetProperty("usage", out JsonElement usage) && usage.TryGetProperty(property, out JsonElement value)
            ? value.GetInt32() : 0;
    private static IntakeCategory? Resolve(string? slug, Dictionary<string, IntakeCategory> categories) =>
        slug is not null && categories.TryGetValue(slug, out IntakeCategory? value) ? value : null;
    private static string Clean(string? value, int length, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim()[..Math.Min(value.Trim().Length, length)];
    private static string? CleanNullable(string? value, int length) => string.IsNullOrWhiteSpace(value) ? null : Clean(value, length, string.Empty);
    private static bool NotBlank(string value) => !string.IsNullOrWhiteSpace(value);

    private static DateOnly? ParsePreferredDeliveryDate(string? value) =>
        DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out DateOnly date)
            ? date
            : null;

    private static DateTimeOffset ParseQuotationDeadline(string? value, DateTimeOffset now)
    {
        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal, out DateTimeOffset deadline)
            && deadline > now)
            return deadline;

        return now.AddDays(2);
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly JsonNode Schema = JsonNode.Parse("""
    {
      "type":"object","additionalProperties":false,
      "required":["inputType","title","projectName","description","primaryCategorySlug","deliveryAddress","deliveryLocality","county","preferredDeliveryDate","quotationDeadline","alternativeProductsAccepted","allowSplit","deliveryRequired","estimatedBudget","buyerNotes","lines","assumptions","uncertainties","clarification"],
      "properties":{
        "inputType":{"type":"string","enum":["FreeText","ShoppingList","ProjectDescription"]},
        "title":{"type":"string"},"projectName":{"type":["string","null"]},"description":{"type":["string","null"]},
        "primaryCategorySlug":{"type":["string","null"]},"deliveryAddress":{"type":"string"},"deliveryLocality":{"type":"string"},"county":{"type":"string"},
        "preferredDeliveryDate":{"type":["string","null"]},"quotationDeadline":{"type":"string"},
        "alternativeProductsAccepted":{"type":"boolean"},"allowSplit":{"type":"boolean"},"deliveryRequired":{"type":"boolean"},
        "estimatedBudget":{"type":["number","null"]},"buyerNotes":{"type":["string","null"]},
        "lines":{"type":"array","items":{"type":"object","additionalProperties":false,"required":["kind","name","specification","quantity","unit","alternativesAccepted","categorySlug"],"properties":{"kind":{"type":"string","enum":["Product","Service"]},"name":{"type":"string"},"specification":{"type":["string","null"]},"quantity":{"type":["number","null"]},"unit":{"anyOf":[{"type":"null"},{"type":"string","enum":["Piece","Pack","Bag","Kilogram","Tonne","Metre","SquareMetre","CubicMetre","Litre","Pallet","Other"]}]},"alternativesAccepted":{"type":"boolean"},"categorySlug":{"type":["string","null"]}}}},
        "assumptions":{"type":"array","items":{"type":"string"}},"uncertainties":{"type":"array","items":{"type":"string"}},
        "clarification":{"anyOf":[{"type":"null"},{"type":"object","additionalProperties":false,"required":["field","question","choices"],"properties":{"field":{"type":"string","enum":["Category","Quantity","Specification","ProjectScope","DeliveryAddress","DeliveryDate"]},"question":{"type":"string"},"choices":{"type":"array","items":{"type":"string"}}}}]}
      }
    }
    """)!;

    private sealed record AiExtraction(string InputType, string Title, string? ProjectName, string? Description,
        string? PrimaryCategorySlug, string DeliveryAddress, string DeliveryLocality, string County,
        string? PreferredDeliveryDate, string? QuotationDeadline, bool AlternativeProductsAccepted,
        bool AllowSplit, bool DeliveryRequired, decimal? EstimatedBudget, string? BuyerNotes,
        IReadOnlyCollection<AiLine> Lines, IReadOnlyCollection<string> Assumptions,
        IReadOnlyCollection<string> Uncertainties,
        AiClarification? Clarification);
    private sealed record AiLine(string Kind, string Name, string? Specification, decimal? Quantity,
        string? Unit, bool AlternativesAccepted, string? CategorySlug);
    private sealed record AiClarification(string Field, string Question, IReadOnlyCollection<string> Choices);
}
