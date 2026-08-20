using Marketplace.Domain.Profiles;

namespace Marketplace.Application.Profiles;

public interface ILocationGeocoder
{
    Task<GeographicLocation> ResolveAsync(LocationInput input, CancellationToken cancellationToken);
}

public sealed record LocationInput(string? Address, string Locality, string County, string Country);

public sealed class LocationResolutionException(string message) : Exception(message);
