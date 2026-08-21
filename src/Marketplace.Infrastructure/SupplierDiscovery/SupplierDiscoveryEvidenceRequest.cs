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
    IReadOnlyCollection<string> WebPrompts,
    string CategoryFocus)
{
    public static SupplierDiscoveryEvidenceRequest Empty { get; } = new(
        "categoria comercială", [], string.Empty, string.Empty, null, null, 0, [], [], string.Empty, [], string.Empty);
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

public sealed record WebEvidenceResult(IReadOnlyCollection<OpenAiWebDiscoveryResponse> Responses, IReadOnlyCollection<string> Warnings)
{
    public int WebSearchCallCount => Responses.Sum(response => response.WebSearchCallCount);
    public long LatencyMs => Responses.Sum(response => response.LatencyMs);
    public int InputTokens => Responses.Sum(response => response.InputTokens);
    public int OutputTokens => Responses.Sum(response => response.OutputTokens);
}

public interface ISupplierEvidenceMerger
{
    Task<JsonObject> MergeAsync(SupplierDiscoveryEvidenceRequest request, GoogleEvidenceResult google, WebEvidenceResult web, CancellationToken cancellationToken);
}
