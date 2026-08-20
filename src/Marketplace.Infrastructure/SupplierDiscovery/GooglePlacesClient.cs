using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Marketplace.Infrastructure.SupplierDiscovery;

public interface IGooglePlacesClient
{
    bool IsAvailable { get; }
    Task<IReadOnlyList<GooglePlaceEvidence>> SearchTextAsync(string textQuery, string regionCode, CancellationToken cancellationToken);
    Task<IReadOnlyList<GooglePlaceEvidence>> SearchNearbyAsync(decimal latitude, decimal longitude, int radiusKm, CancellationToken cancellationToken);
}

// Google Places API (New) Text Search client. Requests a minimal FieldMask only (no reviews/photos/rating/hours/
// phone/website by default) to stay in the lower-cost discovery tier. Returns provisional evidence, never a persisted
// or cached full Google profile; Google's primaryType/types are secondary evidence, not a CompaSpot category.
public sealed partial class GooglePlacesClient(
    HttpClient httpClient,
    IOptions<GooglePlacesOptions> options,
    ILogger<GooglePlacesClient> logger) : IGooglePlacesClient
{
    private readonly GooglePlacesOptions settings = options.Value;

    private static readonly string FieldMask = string.Join(',',
        "places.id", "places.displayName", "places.formattedAddress", "places.addressComponents",
        "places.location", "places.primaryType", "places.primaryTypeDisplayName", "places.types",
        "places.businessStatus", "places.googleMapsUri");

    public bool IsAvailable => settings.Enabled
        && !string.IsNullOrWhiteSpace(settings.ApiKey)
        && Uri.TryCreate(settings.TextSearchEndpoint, UriKind.Absolute, out _);
    public int MinimumTextResultsForNearbyFallback => Math.Clamp(settings.MinimumTextResultsForNearbyFallback, 0, 20);

    public async Task<IReadOnlyList<GooglePlaceEvidence>> SearchTextAsync(
        string textQuery, string regionCode, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, settings.TextSearchEndpoint);
        request.Version = HttpVersion.Version20;
        request.VersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;
        request.Headers.Add("X-Goog-Api-Key", settings.ApiKey);
        request.Headers.Add("X-Goog-FieldMask", FieldMask);
        request.Content = JsonContent.Create(new
        {
            textQuery,
            regionCode,
            languageCode = "ro",
            maxResultCount = Math.Clamp(settings.MaxResults, 1, 20)
        });

        using HttpResponseMessage response = await httpClient.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            LogGooglePlacesFailed(logger, (int)response.StatusCode);
            throw new HttpRequestException($"Google Places returned {(int)response.StatusCode}.");
        }

        using JsonDocument document = JsonDocument.Parse(body);
        if (!document.RootElement.TryGetProperty("places", out JsonElement places) || places.ValueKind != JsonValueKind.Array)
            return [];
        return places.EnumerateArray().Select(MapPlace).ToArray();
    }

    public async Task<IReadOnlyList<GooglePlaceEvidence>> SearchNearbyAsync(
        decimal latitude, decimal longitude, int radiusKm, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, settings.NearbySearchEndpoint);
        request.Version = HttpVersion.Version20;
        request.VersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;
        request.Headers.Add("X-Goog-Api-Key", settings.ApiKey);
        request.Headers.Add("X-Goog-FieldMask", FieldMask);
        request.Content = JsonContent.Create(new
        {
            languageCode = "ro",
            maxResultCount = Math.Clamp(settings.MaxResults, 1, 20),
            rankPreference = "DISTANCE",
            locationRestriction = new
            {
                circle = new
                {
                    center = new { latitude = (double)latitude, longitude = (double)longitude },
                    radius = Math.Clamp(radiusKm, 1, 50) * 1000d
                }
            }
        });

        using HttpResponseMessage response = await httpClient.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            LogGooglePlacesFailed(logger, (int)response.StatusCode);
            throw new HttpRequestException($"Google Places returned {(int)response.StatusCode}.");
        }

        using JsonDocument document = JsonDocument.Parse(body);
        if (!document.RootElement.TryGetProperty("places", out JsonElement places) || places.ValueKind != JsonValueKind.Array)
            return [];
        return places.EnumerateArray().Select(MapPlace).ToArray();
    }

    private static GooglePlaceEvidence MapPlace(JsonElement place) => new(
        Text(place, "id"),
        Nested(place, "displayName", "text"),
        Text(place, "formattedAddress"),
        Component(place, "locality") ?? Component(place, "postal_town") ?? Component(place, "administrative_area_level_2"),
        Component(place, "administrative_area_level_1"),
        place.TryGetProperty("location", out JsonElement location) && location.TryGetProperty("latitude", out JsonElement lat) ? lat.GetDouble() : null,
        place.TryGetProperty("location", out JsonElement location2) && location2.TryGetProperty("longitude", out JsonElement lng) ? lng.GetDouble() : null,
        Text(place, "primaryType"),
        Nested(place, "primaryTypeDisplayName", "text"),
        place.TryGetProperty("types", out JsonElement types) && types.ValueKind == JsonValueKind.Array
            ? types.EnumerateArray().Select(item => item.GetString() ?? string.Empty).Where(value => value.Length > 0).ToArray()
            : [],
        Text(place, "businessStatus"),
        Text(place, "googleMapsUri"));

    private static string Text(JsonElement element, string property) =>
        element.TryGetProperty(property, out JsonElement value) ? value.GetString() ?? string.Empty : string.Empty;

    private static string? Nested(JsonElement element, string property, string child) =>
        element.TryGetProperty(property, out JsonElement value) && value.TryGetProperty(child, out JsonElement text)
            ? text.GetString() : null;

    private static string? Component(JsonElement place, string type)
    {
        if (!place.TryGetProperty("addressComponents", out JsonElement components) || components.ValueKind != JsonValueKind.Array)
            return null;
        foreach (JsonElement component in components.EnumerateArray())
            if (component.TryGetProperty("types", out JsonElement types) && types.ValueKind == JsonValueKind.Array
                && types.EnumerateArray().Any(value => value.GetString() == type))
                return component.TryGetProperty("longText", out JsonElement longText) ? longText.GetString()
                    : component.TryGetProperty("shortText", out JsonElement shortText) ? shortText.GetString() : null;
        return null;
    }

    [LoggerMessage(EventId = 4310, Level = LogLevel.Warning, Message = "Google Places Text Search returned {StatusCode}.")]
    private static partial void LogGooglePlacesFailed(ILogger logger, int statusCode);
}

// Provisional Google Places evidence for a single company. County is the Romanian județ (administrative_area_level_1).
public sealed record GooglePlaceEvidence(
    string PlaceId,
    string? DisplayName,
    string? FormattedAddress,
    string? Locality,
    string? County,
    double? Latitude,
    double? Longitude,
    string? PrimaryType,
    string? PrimaryTypeDisplayName,
    IReadOnlyCollection<string> Types,
    string? BusinessStatus,
    string? GoogleMapsUri);
