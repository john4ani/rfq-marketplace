using Marketplace.Domain.Profiles;

namespace Marketplace.Domain.Tests;

public sealed class ProfileTests
{
    [Fact]
    public void BuyerProfileUpdatePreservesIdentityAndCreationTime()
    {
        Guid id = Guid.NewGuid();
        DateTimeOffset created = DateTimeOffset.UtcNow;
        BuyerProfile profile = BuyerProfile.Create(id, "Ion", BuyerType.Individual, null, null, "0700000000", new("Strada 1", "Iași", "Iași", "România", 47.1m, 27.5m), 30, created);

        profile.Update("Ion SRL", BuyerType.Company, "Ion SRL", "RO123", "0711111111", new("Strada 2", "Cluj-Napoca", "Cluj", "România", 46.7m, 23.5m), 50, created.AddHours(1));

        Assert.Equal(id, profile.Id);
        Assert.Equal(created, profile.CreatedAt);
        Assert.Equal(BuyerType.Company, profile.BuyerType);
        Assert.Equal("Cluj", profile.DefaultCounty);
        Assert.Equal(50, profile.MarketRadiusKm);
    }

    [Fact]
    public void NewSupplierStartsPendingAndActive()
    {
        SupplierProfile profile = SupplierProfile.Create(Guid.NewGuid(), "Materiale SRL", "J00/1/2026", "RO123", "Ana", "ana@example.test", "0700000000", null, new("Strada 1", "Brașov", "Brașov", "România", null, null), 30, null, DateTimeOffset.UtcNow);
        Assert.Equal(SupplierApprovalStatus.Pending, profile.ApprovalStatus);
        Assert.True(profile.IsActive);
    }
}
