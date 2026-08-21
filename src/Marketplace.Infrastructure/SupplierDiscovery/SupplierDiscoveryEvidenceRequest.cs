using System.Text.Json.Nodes;
using Marketplace.Infrastructure.AiIntake;

namespace Marketplace.Infrastructure.SupplierDiscovery;

public sealed record SupplierDiscoveryEvidenceRequest(
    string Category,
    IReadOnlyCollection<Guid> CategoryIds,
    string Locality,
    string County,
    decimal? Latitude,
    decimal? Longitude,
    int SearchRadiusKm,
    IReadOnlyCollection<string> Products,
    IReadOnlyCollection<string> CategoryRecallTerms,
    string GoogleTextQuery,
    string WebPrompt,
    string CategoryFocus)
{
    public static SupplierDiscoveryEvidenceRequest Empty { get; } = new(
        "categoria comercială", [], string.Empty, string.Empty, null, null, 0, [], [], string.Empty, string.Empty, string.Empty);
}

public sealed record CollectedGooglePlace(GooglePlaceEvidence Place, decimal DistanceKm);

public sealed record GoogleEvidenceResult(
    IReadOnlyCollection<CollectedGooglePlace> Places,
    IReadOnlyCollection<string> Warnings);

public interface IGooglePlacesEvidenceCollector
{
    Task<GoogleEvidenceResult> CollectAsync(SupplierDiscoveryEvidenceRequest request, CancellationToken cancellationToken);
}

public interface IWebEvidenceCollector
{
    Task<WebEvidenceResult> CollectAsync(SupplierDiscoveryEvidenceRequest request, CancellationToken cancellationToken);
}

public sealed record WebEvidenceResult(OpenAiWebDiscoveryResponse? Response, IReadOnlyCollection<string> Warnings);

public interface ISupplierEvidenceMerger
{
    JsonObject Merge(SupplierDiscoveryEvidenceRequest request, GoogleEvidenceResult google, WebEvidenceResult web);
}
