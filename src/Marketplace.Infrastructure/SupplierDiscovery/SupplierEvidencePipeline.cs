using System.Text.Json.Nodes;

namespace Marketplace.Infrastructure.SupplierDiscovery;

public sealed record SupplierEvidencePipelineResult(
    JsonObject Evidence,
    GoogleEvidenceResult Google,
    WebEvidenceResult Web);

public interface ISupplierEvidencePipeline
{
    Task<SupplierEvidencePipelineResult> CollectAsync(SupplierDiscoveryEvidenceRequest request, CancellationToken cancellationToken);
}

// The pipeline is deliberately side-effect free: it gathers provisional evidence only.
public sealed class SupplierEvidencePipeline(
    IGooglePlacesEvidenceCollector googleCollector,
    IWebEvidenceCollector webCollector,
    ISupplierEvidenceMerger merger) : ISupplierEvidencePipeline
{
    public async Task<SupplierEvidencePipelineResult> CollectAsync(
        SupplierDiscoveryEvidenceRequest request,
        CancellationToken cancellationToken)
    {
        GoogleEvidenceResult google = await googleCollector.CollectAsync(request, cancellationToken);
        WebEvidenceResult web = await webCollector.CollectAsync(request, cancellationToken);
        return new(merger.Merge(request, google, web), google, web);
    }
}
