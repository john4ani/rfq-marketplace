using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Marketplace.Domain.SupplierDiscovery;
using Marketplace.Application.Profiles;
using Marketplace.Infrastructure.AiIntake;
using Marketplace.Infrastructure.Profiles;

namespace Marketplace.Infrastructure.SupplierDiscovery;

// Keeps source provenance and merges only evidence that identifies the same company.
public sealed class SupplierEvidenceMerger(ILocationGeocoder geocoder) : ISupplierEvidenceMerger
{
    // Google recall remains capped independently by its collector. Web evidence may contain
    // up to ten bounded records and must all reach qualification before the final Top 10.
    private const int MaximumWebEvidence = 20;

    public async Task<JsonObject> MergeAsync(SupplierDiscoveryEvidenceRequest request, GoogleEvidenceResult google, WebEvidenceResult web, CancellationToken cancellationToken)
    {
        CollectedGooglePlace[] googlePlaces = google.Places.ToArray();
        JsonNode? webEvidence = CombineWebEvidence(web.Responses);
        JsonNode? webSources = CombineWebSources(web.Responses);
        EvidenceMergeResult merge = await MergeEvidenceAsync(request, googlePlaces, webEvidence, cancellationToken);
        return new JsonObject
        {
            ["context"] = JsonSerializer.SerializeToNode(request),
            ["googlePlaces"] = JsonSerializer.SerializeToNode(googlePlaces) ?? new JsonArray(),
            ["webEvidence"] = webEvidence,
            ["webSources"] = webSources,
            ["mergedEvidence"] = merge.Evidence,
            ["untrustedWebEvidence"] = merge.UntrustedWebEvidence,
            ["sourceWarnings"] = JsonSerializer.SerializeToNode(google.Warnings.Concat(web.Warnings).Distinct()) ?? new JsonArray()
        };
    }

    private static JsonNode? LimitWebEvidence(JsonNode? evidence)
    {
        if (evidence is not JsonObject root) return evidence?.DeepClone();
        JsonObject limited = (JsonObject)root.DeepClone();
        if (limited["evidence"] is JsonArray items)
            limited["evidence"] = new JsonArray(items.Take(MaximumWebEvidence).Select(item => item?.DeepClone()).ToArray());
        return limited;
    }

    private static JsonObject? CombineWebEvidence(IReadOnlyCollection<OpenAiWebDiscoveryResponse> responses)
    {
        if (responses.Count == 0) return null;
        JsonArray evidence = [];
        foreach (OpenAiWebDiscoveryResponse response in responses)
            if (JsonNode.Parse(response.OutputText) is JsonObject root && root["evidence"] is JsonArray items)
                foreach (JsonNode? item in items.Take(MaximumWebEvidence - evidence.Count)) evidence.Add(item?.DeepClone());
        return new JsonObject { ["evidence"] = evidence };
    }

    private static JsonArray? CombineWebSources(IReadOnlyCollection<OpenAiWebDiscoveryResponse> responses)
    {
        if (responses.Count == 0) return null;
        JsonArray sources = [];
        foreach (OpenAiWebDiscoveryResponse response in responses)
            if (JsonNode.Parse(response.SourcesJson) is JsonArray items)
                foreach (JsonNode? item in items)
                    if (!sources.Any(existing => JsonNode.DeepEquals(existing, item))) sources.Add(item?.DeepClone());
        return sources;
    }

    private async Task<EvidenceMergeResult> MergeEvidenceAsync(SupplierDiscoveryEvidenceRequest request, IReadOnlyCollection<CollectedGooglePlace> googlePlaces, JsonNode? webEvidence, CancellationToken cancellationToken)
    {
        Dictionary<string, JsonObject> merged = new(StringComparer.Ordinal);
        JsonArray untrustedWebEvidence = [];
        void Add(
            string companyName,
            string? locality,
            string? county,
            IEnumerable<string> urls,
            string source,
            JsonNode evidence,
            decimal? distanceKm = null,
            double? latitude = null,
            double? longitude = null)
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
            if (latitude.HasValue) item["latitude"] = latitude.Value;
            if (longitude.HasValue) item["longitude"] = longitude.Value;
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
                "Google Places", JsonSerializer.SerializeToNode(place)!, collected.DistanceKm, place.Latitude, place.Longitude);
        }
        if (webEvidence?["evidence"] is JsonArray webItems)
            foreach (JsonNode? item in webItems.OfType<JsonObject>())
            {
                string name = item["companyName"]?.GetValue<string>() ?? string.Empty;
                string? locality = item["locality"]?.GetValue<string>();
                string? county = item["county"]?.GetValue<string>();
                string[] sourceUrls = item["sourceUrls"] is JsonArray urls
                    ? urls.Select(url => url?.GetValue<string>() ?? string.Empty).Where(url => !string.IsNullOrWhiteSpace(url)).ToArray()
                    : [];
                SourceAssociation[] associations = sourceUrls.Select(url => new SourceAssociation(url, GetSourceAssociation(name, url))).ToArray();
                string[] trustedSourceUrls = associations.Where(item => item.Kind is SourceAssociationKind.Matching or SourceAssociationKind.Generic).Select(item => item.Url).ToArray();
                bool hasConflictingCompanySource = associations.Any(item => item.Kind == SourceAssociationKind.Mismatched);
                if (hasConflictingCompanySource || trustedSourceUrls.Length == 0 ||
                    (associations.All(item => item.Kind == SourceAssociationKind.Generic) && !HasExplicitActivityEvidence((JsonObject)item)))
                {
                    untrustedWebEvidence.Add(new JsonObject
                    {
                        ["companyName"] = name,
                        ["sourceUrls"] = JsonSerializer.SerializeToNode(sourceUrls),
                        ["reason"] = hasConflictingCompanySource
                            ? "Sursa externă identifică o altă firmă."
                            : "Sursa externă nu leagă explicit firma de activitatea declarată."
                    });
                    continue;
                }
                if (!merged.Values.Any(existing => CompanyEvidenceIdentity.IsCompatibleEvidence(name, locality, county,
                    existing["companyName"]?.GetValue<string>() ?? string.Empty, existing["locality"]?.GetValue<string>(), existing["county"]?.GetValue<string>())))
                    {
                        if (string.IsNullOrWhiteSpace(locality) || string.IsNullOrWhiteSpace(county)) continue;
                        try
                        {
                            var location = await geocoder.ResolveAsync(BuildWebLocationInput(locality, county, request.Locality), cancellationToken);
                            decimal distance = GeographicDistance.Kilometers(request.Latitude!.Value, request.Longitude!.Value, location.Latitude!.Value, location.Longitude!.Value);
                            if (distance > request.SearchRadiusKm) continue;
                            Add(name, locality, county, trustedSourceUrls, "OpenAI Web Search", item, distance, (double)location.Latitude!.Value, (double)location.Longitude!.Value);
                            continue;
                        }
                        catch (LocationResolutionException) { continue; }
                    }
                Add(name, locality, county, trustedSourceUrls, "OpenAI Web Search", item);
            }
        return new(new JsonArray(merged.Values.Select(item => (JsonNode)item).ToArray()), untrustedWebEvidence);
    }

    private static SourceAssociationKind GetSourceAssociation(string companyName, string sourceUrl)
    {
        if (string.IsNullOrWhiteSpace(companyName) || !Uri.TryCreate(sourceUrl, UriKind.Absolute, out Uri? uri)) return SourceAssociationKind.Invalid;

        string sourceIdentity = Regex.Replace(Uri.UnescapeDataString(uri.AbsolutePath), @"[^\p{L}\p{N}]+", " ")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(token => !token.All(char.IsDigit) && token is not "FIRMA" and not "COMPANY" and not "EN")
            .Aggregate(string.Empty, (text, token) => $"{text} {token}").Trim();
        string normalizedCompanyName = Regex.Replace(companyName, @"[^\p{L}\p{N}]+", " ");
        if (CompanyEvidenceIdentity.AreSame(normalizedCompanyName, sourceIdentity)) return SourceAssociationKind.Matching;

        // A company-detail route naming a different business is conflicting evidence. A generic
        // directory/list route remains usable only when the structured result explicitly carries activity evidence.
        return uri.AbsolutePath.Contains("/firma/", StringComparison.OrdinalIgnoreCase)
            ? SourceAssociationKind.Mismatched
            : SourceAssociationKind.Generic;
    }

    private static bool HasExplicitActivityEvidence(JsonObject evidence)
    {
        string activity = evidence["activityEvidence"]?.GetValue<string>() ?? string.Empty;
        string? caen = evidence["caenCode"]?.GetValue<string>();
        return !string.IsNullOrWhiteSpace(caen)
            && activity.Contains(caen, StringComparison.OrdinalIgnoreCase)
            && activity.Contains("CAEN", StringComparison.OrdinalIgnoreCase);
    }

    // Web results may supply a complete address in their locality field. Avoid sending
    // duplicated administrative context to the existing geocoder.
    private static LocationInput BuildWebLocationInput(string locality, string county, string fallbackLocality)
    {
        // A street-only Web value has no usable locality. It cannot support a precise address,
        // so use the allowed RFQ-locality fallback rather than inventing missing components.
        string geocodingLocality = IsStreetOnlyLocation(locality) ? fallbackLocality : locality;
        return new(
        null,
        geocodingLocality,
        geocodingLocality.Contains(county, StringComparison.OrdinalIgnoreCase) ? string.Empty : county,
        geocodingLocality.Contains("România", StringComparison.OrdinalIgnoreCase) ? string.Empty : "România");
    }

    private static bool IsStreetOnlyLocation(string value) =>
        value.Contains("strada", StringComparison.OrdinalIgnoreCase)
        && !value.Contains("jude", StringComparison.OrdinalIgnoreCase)
        && !value.Contains("comuna", StringComparison.OrdinalIgnoreCase)
        && !value.Contains("sat", StringComparison.OrdinalIgnoreCase);

    private static void AddDistinct(JsonArray target, string value, StringComparer comparer)
    {
        if (!target.Any(existing => comparer.Equals(existing?.GetValue<string>(), value))) target.Add(value);
    }

    private static string Normalize(string companyName) => companyName.Trim().ToUpperInvariant();

    private sealed record EvidenceMergeResult(JsonArray Evidence, JsonArray UntrustedWebEvidence);
    private sealed record SourceAssociation(string Url, SourceAssociationKind Kind);
    private enum SourceAssociationKind { Invalid, Generic, Matching, Mismatched }
}
