using Marketplace.Domain.Suppliers;

namespace Marketplace.Domain.Tests;

public sealed class SupplierSettingsTests
{
    [Fact]
    public void MaximumRadiusCannotBeSmallerThanNormalRadius()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SupplierDeliverySettings.Create(Guid.NewGuid(), 100, 50, null, false, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void ValidSettingsCanBeUpdated()
    {
        SupplierDeliverySettings settings = SupplierDeliverySettings.Create(Guid.NewGuid(), 50, 100, 500, false, DateTimeOffset.UtcNow);
        settings.Update(75, 150, null, true, DateTimeOffset.UtcNow.AddMinutes(1));
        Assert.Equal(75, settings.NormalDeliveryRadiusKm);
        Assert.Equal(150, settings.MaximumDeliveryRadiusKm);
        Assert.True(settings.NationwideDelivery);
    }
}
