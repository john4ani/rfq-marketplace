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
            return new(null, ["Căutarea web nu este configurată; continuăm cu Google Places."]);

        try
        {
            OpenAiWebDiscoveryResponse response = await transport.SendWebDiscoveryAsync(new(
                request.WebPrompt, "supplier_web_evidence", EvidenceSchema(), request.Locality, request.County, 2400), cancellationToken);
            return new(response, []);
        }
        catch (Exception exception)
        {
            return new(null, [ToFailureMessage(exception)]);
        }
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

    private static JsonNode EvidenceSchema() => JsonNode.Parse("""{"type":"object","additionalProperties":false,"required":["searchIntent","evidence"],"properties":{"searchIntent":{"type":"object","additionalProperties":false,"required":["productOrService","primaryActivity","secondaryActivity","location"],"properties":{"productOrService":{"type":"string"},"primaryActivity":{"type":"string"},"secondaryActivity":{"type":"string"},"location":{"type":"string"}}},"evidence":{"type":"array","maxItems":10,"items":{"type":"object","additionalProperties":false,"required":["companyName","locality","county","website","phone","email","activityEvidence","caenCode","productEvidence","sourceUrls"],"properties":{"companyName":{"type":"string"},"locality":{"type":"string"},"county":{"type":"string"},"website":{"type":"string"},"phone":{"type":"string"},"email":{"type":"string"},"activityEvidence":{"type":"string"},"caenCode":{"type":"string"},"productEvidence":{"type":"string"},"sourceUrls":{"type":"array","items":{"type":"string"}}}}}}}""")!;
}
