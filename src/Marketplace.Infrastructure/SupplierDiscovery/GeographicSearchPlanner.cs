namespace Marketplace.Infrastructure.SupplierDiscovery;

public enum SupplierSearchGeographicTier { Local, Nearby, County }

public sealed record GeographicSearchStep(
    SupplierSearchGeographicTier Tier,
    string Locality,
    string County,
    int RadiusKm,
    IReadOnlyCollection<string> NearbyLocalities);

// Deterministic shared geography policy. Collectors decide how a source executes a step;
// this planner only controls the permitted expansion order.
public static class GeographicSearchPlanner
{
    public static IReadOnlyList<GeographicSearchStep> Plan(SupplierSearchIntent intent) =>
    [
        new(SupplierSearchGeographicTier.Local, intent.Locality, intent.County, 0, []),
        new(SupplierSearchGeographicTier.Nearby, intent.Locality, intent.County, intent.SearchRadiusKm, NearbyLocalities(intent.Locality, intent.County)),
        new(SupplierSearchGeographicTier.County, string.Empty, intent.County, 0, [])
    ];

    // Nearby-city resolution belongs to the next Supplier Discovery rebaseline, after
    // persisted User/Company coordinates are available. No locality chains are encoded here.
    private static IReadOnlyCollection<string> NearbyLocalities(string locality, string county) => [];
}
