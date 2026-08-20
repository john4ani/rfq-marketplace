namespace Marketplace.Infrastructure.SupplierDiscovery;

// Google Maps Platform → Places API (New). Used as the local-business / geographic-recall discovery source in the
// admin supplier discovery Stage 1, alongside (never duplicating) the OpenAI Web Search evidence.
public sealed class GooglePlacesOptions
{
    public const string SectionName = "GooglePlaces";
    public bool Enabled { get; init; } = true;
    public string ApiKey { get; init; } = string.Empty;
    // Text Search (New). Nearby Search is only a bounded fallback and is not wired by default.
    public string TextSearchEndpoint { get; init; } = "https://places.googleapis.com/v1/places:searchText";
    public string NearbySearchEndpoint { get; init; } = "https://places.googleapis.com/v1/places:searchNearby";
    public string GeocodingEndpoint { get; init; } = "https://maps.googleapis.com/maps/api/geocode/json";
    public int RequestTimeoutSeconds { get; init; } = 20;
    // Each bounded query retrieves no more than the five source records retained for qualification.
    public int MaxResults { get; init; } = 5;
    public int MinimumTextResultsForNearbyFallback { get; init; } = 3;
}
