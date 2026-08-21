using System.Text.Json;
using System.Text.Json.Nodes;
using Marketplace.Domain.SupplierDiscovery;

namespace Marketplace.Infrastructure.SupplierDiscovery;

// Keeps source provenance and merges only evidence that identifies the same company.
public sealed class SupplierEvidenceMerger : ISupplierEvidenceMerger
{
    private const int MaximumEvidencePerSource = 5;

    public JsonObject Merge(SupplierDiscoveryEvidenceRequest request, GoogleEvidenceResult google, WebEvidenceResult web)
    {
        CollectedGooglePlace[] googlePlaces = google.Places.Take(MaximumEvidencePerSource).ToArray();
        JsonNode? webEvidence = web.Response is null ? null : LimitWebEvidence(JsonNode.Parse(web.Response.OutputText));
        JsonNode? webSources = web.Response is null ? null : JsonNode.Parse(web.Response.SourcesJson);
        return new JsonObject
        {
            ["context"] = JsonSerializer.SerializeToNode(request),
            ["googlePlaces"] = JsonSerializer.SerializeToNode(googlePlaces) ?? new JsonArray(),
            ["webEvidence"] = webEvidence,
            ["webSources"] = webSources,
            ["mergedEvidence"] = MergeEvidence(googlePlaces, webEvidence),
            ["sourceWarnings"] = JsonSerializer.SerializeToNode(google.Warnings.Concat(web.Warnings).Distinct()) ?? new JsonArray()
        };
    }

    private static JsonNode? LimitWebEvidence(JsonNode? evidence)
    {
        if (evidence is not JsonObject root) return evidence?.DeepClone();
        JsonObject limited = (JsonObject)root.DeepClone();
        if (limited["evidence"] is JsonArray items)
            limited["evidence"] = new JsonArray(items.Take(MaximumEvidencePerSource).Select(item => item?.DeepClone()).ToArray());
        return limited;
    }

    private static JsonArray MergeEvidence(IReadOnlyCollection<CollectedGooglePlace> googlePlaces, JsonNode? webEvidence)
    {
        Dictionary<string, JsonObject> merged = new(StringComparer.Ordinal);
        void Add(string companyName, string? locality, string? county, IEnumerable<string> urls, string source, JsonNode evidence, decimal? distanceKm = null)
        {
            if (string.IsNullOrWhiteSpace(companyName)) return;
            string key = $"{Normalize(companyName)}|{(locality ?? string.Empty).Trim().ToUpperInvariant()}|{(county ?? string.Empty).Trim().ToUpperInvariant()}";
            JsonObject? item = merged.TryGetValue(key, out JsonObject? exact) ? exact : merged.Values.FirstOrDefault(existing =>
                CompanyEvidenceIdentity.IsCompatibleEvidence(companyName, locality, county,
                    existing["companyName"]?.GetValue<string>() ?? string.Empty,
                    existing["locality"]?.GetValue<string>(), existing["county"]?.GetValue<string>()));
            if (item is null)
            {
                item = new JsonObject { ["companyName"] = companyName, ["locality"] = locality ?? string.Empty, ["county"] = county ?? string.Empty, ["sources"] = new JsonArray(), ["evidence"] = new JsonArray() };
                merged.Add(key, item);
            }
            AddDistinct(item["sources"]!.AsArray(), source, StringComparer.Ordinal);
            if (distanceKm.HasValue) item["distanceKm"] = distanceKm.Value;
            JsonArray sourceUrls = item["sourceUrls"] as JsonArray ?? new JsonArray();
            item["sourceUrls"] = sourceUrls;
            foreach (string url in urls.Where(url => !string.IsNullOrWhiteSpace(url)).Distinct(StringComparer.OrdinalIgnoreCase))
                AddDistinct(sourceUrls, url, StringComparer.OrdinalIgnoreCase);
            item["evidence"]!.AsArray().Add(evidence.DeepClone());
        }

        foreach (CollectedGooglePlace collected in googlePlaces)
        {
            GooglePlaceEvidence place = collected.Place;
            Add(place.DisplayName ?? string.Empty, place.Locality, place.County, [place.GoogleMapsUri ?? string.Empty],
                "Google Places", JsonSerializer.SerializeToNode(place)!, collected.DistanceKm);
        }
        if (webEvidence?["evidence"] is JsonArray webItems)
            foreach (JsonNode? item in webItems.OfType<JsonObject>())
            {
                string name = item["companyName"]?.GetValue<string>() ?? string.Empty;
                string? locality = item["locality"]?.GetValue<string>();
                string? county = item["county"]?.GetValue<string>();
                if (!merged.Values.Any(existing => CompanyEvidenceIdentity.IsCompatibleEvidence(name, locality, county,
                    existing["companyName"]?.GetValue<string>() ?? string.Empty, existing["locality"]?.GetValue<string>(), existing["county"]?.GetValue<string>())))
                    continue;
                Add(name, locality, county, item["sourceUrls"] is JsonArray urls ? urls.Select(url => url?.GetValue<string>() ?? string.Empty) : [], "OpenAI Web Search", item);
            }
        return new JsonArray(merged.Values.Select(item => (JsonNode)item).ToArray());
    }

    private static void AddDistinct(JsonArray target, string value, StringComparer comparer)
    {
        if (!target.Any(existing => comparer.Equals(existing?.GetValue<string>(), value))) target.Add(value);
    }

    private static string Normalize(string companyName) => companyName.Trim().ToUpperInvariant();
}
