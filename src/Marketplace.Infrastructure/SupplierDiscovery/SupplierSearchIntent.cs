namespace Marketplace.Infrastructure.SupplierDiscovery;

using Marketplace.Domain.Rfqs;

public sealed record SupplierSearchIntent(
    string CategoryName,
    CategoryDiscoveryProfile Profile,
    IReadOnlyCollection<string> RequestedProducts,
    string Locality,
    string County,
    decimal? Latitude,
    decimal? Longitude,
    int SearchRadiusKm);

public interface ISupplierSearchIntentBuilder
{
    SupplierSearchIntent Build(Rfq need, string categoryName, CategoryDiscoveryProfile profile, IReadOnlyCollection<string> products);
}

public sealed class SupplierSearchIntentBuilder : ISupplierSearchIntentBuilder
{
    public SupplierSearchIntent Build(Rfq need, string categoryName, CategoryDiscoveryProfile profile, IReadOnlyCollection<string> products) =>
        new(categoryName, profile, products, need.DeliveryLocality, need.County, need.Latitude, need.Longitude, need.SupplierSearchRadiusKm);
}
