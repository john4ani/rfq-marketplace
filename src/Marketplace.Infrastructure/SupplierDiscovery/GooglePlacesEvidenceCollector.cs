using Marketplace.Infrastructure.AiIntake;
using Marketplace.Infrastructure.Profiles;

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
            if (!request.Latitude.HasValue || !request.Longitude.HasValue || request.SearchRadiusKm < 1)
                return new([], ["Cererea nu are coordonate sau rază de căutare utilizabile."]);
            List<CollectedGooglePlace> collected = [];
            foreach (GooglePlaceEvidence place in await client.SearchTextAsync(request.GoogleTextQuery, "RO", request.Latitude.Value, request.Longitude.Value, request.SearchRadiusKm, cancellationToken))
            {
                if (!place.Latitude.HasValue || !place.Longitude.HasValue) continue;
                decimal distanceKm = GeographicDistance.Kilometers(request.Latitude.Value, request.Longitude.Value, (decimal)place.Latitude.Value, (decimal)place.Longitude.Value);
                if (distanceKm <= request.SearchRadiusKm && !collected.Any(existing => existing.Place.PlaceId == place.PlaceId)) collected.Add(new(place, distanceKm));
                if (collected.Count >= MaximumEvidence) break;
            }
            return new(collected, []);
        }
        catch (Exception)
        {
            return new([], ["Google Places nu este disponibil momentan; continuăm cu dovezile web."]);
        }
    }


}
