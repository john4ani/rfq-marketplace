using System.Text.Json;
using System.Text.Json.Nodes;
using Marketplace.Infrastructure.AiIntake;

namespace Marketplace.Infrastructure.SupplierDiscovery;

public sealed class WebEvidenceCollector(OpenAiResponsesTransport transport) : IWebEvidenceCollector
{
    private const int MaximumEvidence = 5;

    public async Task<WebEvidenceResult> CollectAsync(
        SupplierDiscoveryEvidenceRequest request,
        CancellationToken cancellationToken)
    {
        if (!transport.IsAvailable)
            return new(null, ["Căutarea web nu este configurată; continuăm cu Google Places."]);

        try
        {
            OpenAiWebDiscoveryResponse response = await transport.SendWebDiscoveryAsync(new(
                request.WebPrompt, "supplier_web_evidence", EvidenceSchema(), request.Locality, request.County, 2400), cancellationToken);
            if (EvidenceCount(response.OutputText) < MaximumEvidence)
            {
                OpenAiWebDiscoveryResponse expanded = await transport.SendWebDiscoveryAsync(new(
                    BuildExpansionPrompt(request), "supplier_web_evidence", EvidenceSchema(), request.Locality, request.County, 2400), cancellationToken);
                response = Combine(response, expanded);
            }
            return new(response, []);
        }
        catch (Exception exception)
        {
            return new(null, [ToFailureMessage(exception)]);
        }
    }

    private static int EvidenceCount(string output)
    {
        using JsonDocument document = JsonDocument.Parse(output);
        return document.RootElement.TryGetProperty("evidence", out JsonElement evidence) && evidence.ValueKind == JsonValueKind.Array
            ? evidence.GetArrayLength() : 0;
    }

    private static OpenAiWebDiscoveryResponse Combine(OpenAiWebDiscoveryResponse local, OpenAiWebDiscoveryResponse expanded)
    {
        JsonObject combined = JsonNode.Parse(local.OutputText)?.AsObject()
            ?? throw new InvalidOperationException("Web discovery did not return structured evidence.");
        JsonArray target = combined["evidence"]?.AsArray()
            ?? throw new InvalidOperationException("Web discovery did not return structured evidence.");
        JsonArray additions = JsonNode.Parse(expanded.OutputText)?["evidence"]?.AsArray()
            ?? throw new InvalidOperationException("Web discovery did not return structured evidence.");
        foreach (JsonNode? item in additions)
            if (target.Count < MaximumEvidence) target.Add(item?.DeepClone());
        JsonArray sources = [];
        foreach (string json in new[] { local.SourcesJson, expanded.SourcesJson })
            if (JsonNode.Parse(json) is JsonArray array)
                foreach (JsonNode? item in array) sources.Add(item?.DeepClone());
        return new(combined.ToJsonString(), sources.ToJsonString(), local.WebSearchCallCount + expanded.WebSearchCallCount,
            local.LatencyMs + expanded.LatencyMs, local.InputTokens + expanded.InputTokens, local.OutputTokens + expanded.OutputTokens);
    }

    private static string BuildExpansionPrompt(SupplierDiscoveryEvidenceRequest request) => $"""
        Caută dovezi provizorii despre furnizori pentru categoria „{request.Category}”. Localitatea inițială „{request.Locality}” nu a produs suficiente firme relevante.
        Extinde numai în zona apropiată, în raza comercială de {request.SearchRadiusKm} km. Dacă încă lipsesc rezultate, extinde în județul „{request.County}”. Nu căuta în alte județe și nu repeta firmele deja găsite local. Returnează maximum cinci dovezi, cu URL-uri, fără a considera datele verificate.
        Produse/servicii: {string.Join("; ", request.Products)}.
        """;

    private static string ToFailureMessage(Exception exception) => exception switch
    {
        SupplierWebDiscoveryUnavailableException unavailable => unavailable.Message,
        HttpRequestException { StatusCode: System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden }
            => "Căutarea web nu este autorizată. Verifică cheia API configurată pentru acest API.",
        HttpRequestException { StatusCode: System.Net.HttpStatusCode.TooManyRequests }
            => "Limita temporară a serviciului AI a fost atinsă. Încearcă din nou peste câteva minute.",
        OperationCanceledException => "Căutarea web a depășit timpul disponibil. Încearcă din nou.",
        InvalidOperationException { Message: "Web discovery did not return structured evidence." }
            => "Căutarea pe web a răspuns incomplet. Încearcă din nou.",
        _ => "Nu am putut căuta furnizori pe web. Încearcă din nou."
    };

    private static JsonNode EvidenceSchema() => JsonNode.Parse("""{"type":"object","additionalProperties":false,"required":["searchIntent","evidence"],"properties":{"searchIntent":{"type":"object","additionalProperties":false,"required":["productOrService","primaryActivity","secondaryActivity","location"],"properties":{"productOrService":{"type":"string"},"primaryActivity":{"type":"string"},"secondaryActivity":{"type":"string"},"location":{"type":"string"}}},"evidence":{"type":"array","maxItems":10,"items":{"type":"object","additionalProperties":false,"required":["companyName","locality","county","website","phone","email","activityEvidence","caenCode","productEvidence","sourceUrls"],"properties":{"companyName":{"type":"string"},"locality":{"type":"string"},"county":{"type":"string"},"website":{"type":"string"},"phone":{"type":"string"},"email":{"type":"string"},"activityEvidence":{"type":"string"},"caenCode":{"type":"string"},"productEvidence":{"type":"string"},"sourceUrls":{"type":"array","items":{"type":"string"}}}}}}}""")!;
}
