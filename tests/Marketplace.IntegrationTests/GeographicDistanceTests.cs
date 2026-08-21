using Marketplace.Infrastructure.Profiles;

namespace Marketplace.IntegrationTests;

public sealed class GeographicDistanceTests
{
    [Fact]
    public void UsesRealDistanceForThirtyKilometerMarketRadius()
    {
        decimal tenKm = GeographicDistance.Kilometers(0m, 0m, 0m, 0.09m);
        decimal fortyKm = GeographicDistance.Kilometers(0m, 0m, 0m, 0.36m);

        Assert.InRange(tenKm, 9m, 11m);
        Assert.True(tenKm <= 30m);
        Assert.InRange(fortyKm, 39m, 41m);
        Assert.True(fortyKm > 30m);
        Assert.True(fortyKm <= 50m);
    }
}
