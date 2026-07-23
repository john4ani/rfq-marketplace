using Marketplace.Application.Profiles;
using Marketplace.Domain.Profiles;
using Marketplace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Infrastructure.Profiles;

public sealed class ProfileService(MarketplaceDbContext dbContext, TimeProvider timeProvider) : IProfileService
{
    public Task<BuyerProfile?> GetBuyerAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.BuyerProfiles.AsNoTracking().SingleOrDefaultAsync(profile => profile.Id == id, cancellationToken);

    public async Task<BuyerProfile> SaveBuyerAsync(Guid id, BuyerProfileInput input, CancellationToken cancellationToken)
    {
        BuyerProfile? profile = await dbContext.BuyerProfiles.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        DateTimeOffset now = timeProvider.GetUtcNow();
        if (profile is null)
        {
            profile = BuyerProfile.Create(id, input.DisplayName, input.BuyerType, input.CompanyName, input.TaxIdentifier, input.PhoneNumber, input.DefaultCity, input.DefaultCounty, now);
            dbContext.BuyerProfiles.Add(profile);
        }
        else profile.Update(input.DisplayName, input.BuyerType, input.CompanyName, input.TaxIdentifier, input.PhoneNumber, input.DefaultCity, input.DefaultCounty, now);
        await dbContext.SaveChangesAsync(cancellationToken);
        return profile;
    }

    public Task<SupplierProfile?> GetSupplierAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.SupplierProfiles.AsNoTracking().SingleOrDefaultAsync(profile => profile.Id == id, cancellationToken);

    public async Task<SupplierProfile> SaveSupplierAsync(Guid id, SupplierProfileInput input, CancellationToken cancellationToken)
    {
        SupplierProfile? profile = await dbContext.SupplierProfiles.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        DateTimeOffset now = timeProvider.GetUtcNow();
        if (profile is null)
        {
            profile = SupplierProfile.Create(id, input.TradingName, input.CompanyRegistrationNumber, input.TaxIdentifier, input.ContactPerson, input.ContactEmail, input.PhoneNumber, input.Website, input.Address, input.City, input.County, input.Latitude, input.Longitude, input.Description, now);
            dbContext.SupplierProfiles.Add(profile);
        }
        else profile.Update(input.TradingName, input.CompanyRegistrationNumber, input.TaxIdentifier, input.ContactPerson, input.ContactEmail, input.PhoneNumber, input.Website, input.Address, input.City, input.County, input.Latitude, input.Longitude, input.Description, now);
        await dbContext.SaveChangesAsync(cancellationToken);
        return profile;
    }
}
