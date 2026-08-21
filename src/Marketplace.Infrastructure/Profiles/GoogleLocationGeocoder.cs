using System.Text.Json;
using Marketplace.Application.Profiles;
using Marketplace.Domain.Profiles;
using Marketplace.Infrastructure.SupplierDiscovery;
using Microsoft.Extensions.Options;

namespace Marketplace.Infrastructure.Profiles;

public sealed class GoogleLocationGeocoder(HttpClient httpClient, IOptions<GooglePlacesOptions> options) : ILocationGeocoder
{
    private const string DefaultGeocodingEndpoint = "https://maps.googleapis.com/maps/api/geocode/json";
    private readonly GooglePlacesOptions settings = options.Value;

    public async Task<GeographicLocation> ResolveAsync(LocationInput input, CancellationToken cancellationToken)
    {
        if (!settings.Enabled || string.IsNullOrWhiteSpace(settings.ApiKey))
            throw new LocationResolutionException("Serviciul de localizare nu este configurat. Încearcă mai târziu.");

        string address = string.Join(", ", new[] { input.Address, input.Locality, input.County, input.Country }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
        string endpoint = string.IsNullOrWhiteSpace(settings.GeocodingEndpoint)
            ? DefaultGeocodingEndpoint
            : settings.GeocodingEndpoint;
        Uri uri = new($"{endpoint}?address={Uri.EscapeDataString(address)}&key={Uri.EscapeDataString(settings.ApiKey)}&language=ro&region=ro");
        using HttpResponseMessage response = await httpClient.GetAsync(uri, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new LocationResolutionException("Nu am putut confirma locația. Verifică adresa și încearcă din nou.");
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        if (!document.RootElement.TryGetProperty("status", out JsonElement status) || status.GetString() != "OK"
            || !document.RootElement.TryGetProperty("results", out JsonElement results) || results.GetArrayLength() == 0)
            throw new LocationResolutionException("Locația nu a putut fi găsită. Verifică localitatea, județul și adresa.");
        JsonElement location = results[0].GetProperty("geometry").GetProperty("location");
        return new GeographicLocation(input.Address, input.Locality, input.County, input.Country,
            location.GetProperty("lat").GetDecimal(), location.GetProperty("lng").GetDecimal());
    }
}
