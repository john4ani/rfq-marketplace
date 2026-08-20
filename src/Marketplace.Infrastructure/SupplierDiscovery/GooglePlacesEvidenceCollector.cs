using Marketplace.Infrastructure.AiIntake;

namespace Marketplace.Infrastructure.SupplierDiscovery;

// Google Places provides geographic recall only. Product/activity proof is added by web evidence and the qualifier.
public sealed class GooglePlacesEvidenceCollector(IGooglePlacesClient client) : IGooglePlacesEvidenceCollector
{
    private const int MaximumEvidence = 5;

    public async Task<GoogleEvidenceResult> CollectAsync(
        SupplierDiscoveryEvidenceRequest request,
        CancellationToken cancellationToken)
    {
        if (!client.IsAvailable)
            return new([], ["Google Places nu este configurat; continuăm cu dovezile web."]);

        try
        {
            List<CollectedGooglePlace> collected = [];
            foreach (GeographicSearchStep step in GeographicSearchPlanner.Plan(ToIntent(request)))
            {
                if (collected.Count >= MaximumEvidence) break;
                double? discoveredLatitude = collected.Select(item => item.Place.Latitude).FirstOrDefault(value => value.HasValue);
                double? discoveredLongitude = collected.Select(item => item.Place.Longitude).FirstOrDefault(value => value.HasValue);
                decimal? latitude = request.Latitude ?? (discoveredLatitude.HasValue ? (decimal?)discoveredLatitude.Value : null);
                decimal? longitude = request.Longitude ?? (discoveredLongitude.HasValue ? (decimal?)discoveredLongitude.Value : null);
                IReadOnlyList<GooglePlaceEvidence> places = await SearchStepAsync(request, step, latitude, longitude, cancellationToken);
                foreach (GooglePlaceEvidence place in places)
                {
                    if (!IsCommerciallyRelevant(place, request)
                        || collected.Any(existing => string.Equals(existing.Place.PlaceId, place.PlaceId, StringComparison.Ordinal)))
                        continue;
                    collected.Add(new(place, step.Tier));
                    if (collected.Count >= MaximumEvidence) break;
                }
            }
            return new(collected, []);
        }
        catch (Exception)
        {
            return new([], ["Google Places nu este disponibil momentan; continuăm cu dovezile web."]);
        }
    }

    private async Task<IReadOnlyList<GooglePlaceEvidence>> SearchStepAsync(
        SupplierDiscoveryEvidenceRequest request,
        GeographicSearchStep step,
        decimal? latitude,
        decimal? longitude,
        CancellationToken cancellationToken)
    {
        return step.Tier switch
        {
            SupplierSearchGeographicTier.Local => await SearchLocalAsync(request, cancellationToken),
            SupplierSearchGeographicTier.Nearby => await SearchNearbyAsync(request, step, latitude, longitude, cancellationToken),
            SupplierSearchGeographicTier.County => await client.SearchTextAsync($"{request.Category} {request.County} România", "RO", cancellationToken),
            _ => []
        };
    }

    private async Task<IReadOnlyList<GooglePlaceEvidence>> SearchLocalAsync(
        SupplierDiscoveryEvidenceRequest request,
        CancellationToken cancellationToken)
    {
        List<GooglePlaceEvidence> results = [];
        foreach (string query in LocalQueries(request))
        {
            foreach (GooglePlaceEvidence place in await client.SearchTextAsync(query, "RO", cancellationToken))
            {
                if (!string.Equals(place.Locality, request.Locality, StringComparison.OrdinalIgnoreCase)
                    || results.Any(existing => string.Equals(existing.PlaceId, place.PlaceId, StringComparison.Ordinal)))
                    continue;
                results.Add(place);
            }
        }
        return results;
    }

    private async Task<IReadOnlyList<GooglePlaceEvidence>> SearchNearbyAsync(
        SupplierDiscoveryEvidenceRequest request,
        GeographicSearchStep step,
        decimal? latitude,
        decimal? longitude,
        CancellationToken cancellationToken)
    {
        List<GooglePlaceEvidence> results = [];
        foreach (string nearbyLocality in step.NearbyLocalities)
            results.AddRange(await client.SearchTextAsync($"{request.Category} {string.Join(" ", request.Products)} {nearbyLocality} {request.County} România", "RO", cancellationToken));
        if (latitude.HasValue && longitude.HasValue)
            results.AddRange(await client.SearchNearbyAsync(latitude.Value, longitude.Value, step.RadiusKm, cancellationToken));
        return results.Where(place => step.NearbyLocalities.Count == 0
                || step.NearbyLocalities.Any(locality => string.Equals(place.Locality, locality, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
    }

    private static IEnumerable<string> LocalQueries(SupplierDiscoveryEvidenceRequest request)
    {
        yield return request.GoogleTextQuery;
        if (!string.IsNullOrWhiteSpace(request.Locality)) yield return $"{request.Category} {request.Locality} România";
    }

    private static bool IsCommerciallyRelevant(GooglePlaceEvidence place, SupplierDiscoveryEvidenceRequest request)
    {
        string evidence = string.Join(' ', new[] { place.DisplayName, place.PrimaryType, place.PrimaryTypeDisplayName }
            .Concat(place.Types).Where(value => !string.IsNullOrWhiteSpace(value))).ToUpperInvariant();
        if (request.Category.Contains("Piese auto", StringComparison.OrdinalIgnoreCase)
            && (place.Types.Contains("auto_parts_store", StringComparer.OrdinalIgnoreCase)
                || string.Equals(place.PrimaryType, "auto_parts_store", StringComparison.OrdinalIgnoreCase)))
            return true;
        string[] terms = new[] { request.Category }.Concat(request.Products)
            .SelectMany(value => value.ToUpperInvariant().Split([' ', '-', '/', ','], StringSplitOptions.RemoveEmptyEntries))
            .Where(term => term.Length >= 4 && term is not "PENTRU" and not "CARE" and not "DIN" and not "AUTO")
            .Distinct(StringComparer.Ordinal).ToArray();
        return terms.Any(evidence.Contains);
    }

    private static SupplierSearchIntent ToIntent(SupplierDiscoveryEvidenceRequest request) => new(
        request.Category,
        new(request.Category, [], [], [], [], [], request.CategoryFocus),
        request.Products, request.Locality, request.County, request.Latitude, request.Longitude, request.SearchRadiusKm);
}
