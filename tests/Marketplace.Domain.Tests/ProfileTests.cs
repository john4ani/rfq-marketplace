using Marketplace.Domain.Profiles;

namespace Marketplace.Domain.Tests;

public sealed class ProfileTests
{
    [Fact]
    public void BuyerProfileUpdatePreservesIdentityAndCreationTime()
    {
        Guid id = Guid.NewGuid();
        DateTimeOffset created = DateTimeOffset.UtcNow;
        BuyerProfile profile = BuyerProfile.Create(id, "Ion", BuyerType.Individual, null, null, "0700000000", "Iași", "Iași", created);

        profile.Update("Ion SRL", BuyerType.Company, "Ion SRL", "RO123", "0711111111", "Cluj-Napoca", "Cluj", created.AddHours(1));

        Assert.Equal(id, profile.Id);
        Assert.Equal(created, profile.CreatedAt);
        Assert.Equal(BuyerType.Company, profile.BuyerType);
        Assert.Equal("Cluj", profile.DefaultCounty);
    }

    [Fact]
    public void NewSupplierStartsPendingAndActive()
    {
        SupplierProfile profile = SupplierProfile.Create(Guid.NewGuid(), "Materiale SRL", "J00/1/2026", "RO123", "Ana", "ana@example.test", "0700000000", null, "Strada 1", "Brașov", "Brașov", null, null, null, DateTimeOffset.UtcNow);
        Assert.Equal(SupplierApprovalStatus.Pending, profile.ApprovalStatus);
        Assert.True(profile.IsActive);
    }
}
