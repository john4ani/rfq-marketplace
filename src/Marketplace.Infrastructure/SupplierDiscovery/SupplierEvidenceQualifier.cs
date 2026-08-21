using System.Text.Json.Nodes;
using Marketplace.Infrastructure.AiIntake;

namespace Marketplace.Infrastructure.SupplierDiscovery;

public interface ISupplierEvidenceQualifier
{
    Task<OpenAiStructuredResponse> QualifyAsync(SupplierQualificationRequest request, CancellationToken cancellationToken);
}

public sealed record SupplierQualificationRequest(string EvidenceJson, string Category, string Locality, string Guidance);

// Final relevance decision. This boundary deliberately uses the tool-free structured transport only.
public sealed class SupplierEvidenceQualifier(OpenAiResponsesTransport transport) : ISupplierEvidenceQualifier
{
    public Task<OpenAiStructuredResponse> QualifyAsync(SupplierQualificationRequest request, CancellationToken cancellationToken) =>
        transport.SendAsync(new(
            $"""
            Califică exclusiv dovezile fuzionate din „mergedEvidence”. Nu folosi instrumente și nu inventa fapte.
            Eligibilitatea comercială este obligatorie înaintea clasării. Categoria principală „{request.Category}” are prioritate foarte mare; apropierea nu compensează activitatea greșită. Include doar firme care corespund unei dovezi fuzionate cu coordonate utilizabile; backend-ul face ulterior clasarea finală, determinist, după relevanța categoriei și DistanceKm.
            Google Places oferă recall local; primaryType/types sunt dovezi secundare și nu confirmă produsul. Website-ul, activitatea, CAEN și dovezile de produs confirmă sau resping potrivirea.
            {request.Guidance}
            Returnează maximum 25 candidates și toate falsurile pozitive în rejectedResults, cu motiv concret și URL-uri. Nu coborî pragul pentru a umple lista.
        """,
            request.EvidenceJson, "supplier_web_qualification", Schema, Guid.NewGuid(), 4000), cancellationToken);

    private static readonly JsonNode Schema = JsonNode.Parse("""{"type":"object","additionalProperties":false,"required":["candidates","rejectedResults"],"properties":{"candidates":{"type":"array","maxItems":25,"items":{"type":"object","additionalProperties":false,"required":["companyName","locality","county","website","phone","email","activityEvidence","caenCode","productEvidence","geographicRelevance","confidence","sourceUrls","whyRelevant"],"properties":{"companyName":{"type":"string"},"locality":{"type":"string"},"county":{"type":"string"},"website":{"type":"string"},"phone":{"type":"string"},"email":{"type":"string"},"activityEvidence":{"type":"string"},"caenCode":{"type":"string"},"productEvidence":{"type":"string"},"geographicRelevance":{"type":"string","enum":["local","nearby","county","distant","unknown"]},"confidence":{"type":"string","enum":["high","medium","low"]},"sourceUrls":{"type":"array","items":{"type":"string"}},"whyRelevant":{"type":"string"}}}},"rejectedResults":{"type":"array","items":{"type":"object","additionalProperties":false,"required":["companyName","reason","sourceUrls"],"properties":{"companyName":{"type":"string"},"reason":{"type":"string"},"sourceUrls":{"type":"array","items":{"type":"string"}}}}}}}""")!;
}
