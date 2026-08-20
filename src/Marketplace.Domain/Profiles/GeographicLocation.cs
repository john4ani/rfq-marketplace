namespace Marketplace.Domain.Profiles;

// Shared persisted-location shape. Each owning profile keeps its own independent values.
public sealed record GeographicLocation(
    string? Address,
    string Locality,
    string County,
    string Country,
    decimal? Latitude,
    decimal? Longitude)
{
    public bool HasCoordinates => Latitude.HasValue && Longitude.HasValue;
}
