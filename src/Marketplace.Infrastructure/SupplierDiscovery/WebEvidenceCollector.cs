using System.Text.Json;
using System.Text.Json.Nodes;
using Marketplace.Infrastructure.AiIntake;

namespace Marketplace.Infrastructure.SupplierDiscovery;

public sealed class WebEvidenceCollector(OpenAiResponsesTransport transport) : IWebEvidenceCollector
{
    public async Task<WebEvidenceResult> CollectAsync(
        SupplierDiscoveryEvidenceRequest request,
        CancellationToken cancellationToken)
    {
        if (!transport.IsAvailable)
            return new([], ["Căutarea web nu este configurată; continuăm cu Google Places."]);

        List<OpenAiWebDiscoveryResponse> responses = [];
        List<string> warnings = [];
        foreach (string prompt in request.WebPrompts.Where(prompt => !string.IsNullOrWhiteSpace(prompt)).Take(2))
        {
            try
            {
                OpenAiWebDiscoveryResponse response = await transport.SendWebDiscoveryAsync(new(
                    prompt, "supplier_web_evidence", EvidenceSchema(), request.Locality, request.County, 5000), cancellationToken);
                responses.Add(response);
                if (UsableCompanyEvidenceCount(responses) >= 10) break;
            }
            catch (Exception exception)
            {
                warnings.Add(ToFailureMessage(exception));
            }
        }
        return new(responses, warnings);
    }

    private static int UsableCompanyEvidenceCount(IEnumerable<OpenAiWebDiscoveryResponse> responses)
    {
        HashSet<string> companies = new(StringComparer.OrdinalIgnoreCase);
        foreach (OpenAiWebDiscoveryResponse response in responses)
        {
            try
            {
                JsonArray? evidence = JsonNode.Parse(response.OutputText)?["evidence"]?.AsArray();
                if (evidence is null) continue;
                foreach (JsonObject item in evidence.OfType<JsonObject>())
                {
                    string name = item["companyName"]?.GetValue<string>()?.Trim() ?? string.Empty;
                    bool hasSource = item["sourceUrls"] is JsonArray urls && urls.Any(url => !string.IsNullOrWhiteSpace(url?.GetValue<string>()));
                    if (!string.IsNullOrWhiteSpace(name) && hasSource) companies.Add(name);
                }
            }
            catch (JsonException) { }
        }
        return companies.Count;
    }

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

    private static JsonNode EvidenceSchema() => JsonNode.Parse("""{"type":"object","additionalProperties":false,"required":["searchIntent","evidence"],"properties":{"searchIntent":{"type":"object","additionalProperties":false,"required":["productOrService","primaryActivity","secondaryActivity","location"],"properties":{"productOrService":{"type":"string"},"primaryActivity":{"type":"string"},"secondaryActivity":{"type":"string"},"location":{"type":"string"}}},"evidence":{"type":"array","maxItems":20,"items":{"type":"object","additionalProperties":false,"required":["companyName","locality","county","website","phone","email","activityEvidence","caenCode","productEvidence","sourceUrls"],"properties":{"companyName":{"type":"string"},"locality":{"type":"string"},"county":{"type":"string"},"website":{"type":"string"},"phone":{"type":"string"},"email":{"type":"string"},"activityEvidence":{"type":"string"},"caenCode":{"type":"string"},"productEvidence":{"type":"string"},"sourceUrls":{"type":"array","items":{"type":"string"}}}}}}}""")!;
}
