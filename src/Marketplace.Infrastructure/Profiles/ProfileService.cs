using Marketplace.Application.Profiles;
using Marketplace.Domain.Profiles;
using Marketplace.Infrastructure.Persistence;
using Marketplace.Domain.Trust;
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
            profile = BuyerProfile.Create(id, input.DisplayName, input.BuyerType, input.CompanyName, input.TaxIdentifier, input.PhoneNumber, input.DefaultCity, input.DefaultCounty, input.MarketRadiusKm, now);
            dbContext.BuyerProfiles.Add(profile);
        }
        else profile.Update(input.DisplayName, input.BuyerType, input.CompanyName, input.TaxIdentifier, input.PhoneNumber, input.DefaultCity, input.DefaultCounty, input.MarketRadiusKm, now);
        dbContext.AccountAuditEvents.Add(AccountAuditEvent.Create(id, id, AccountAuditEventType.ProfileSaved, "Profil de cumpărător salvat.", now));
        await dbContext.SaveChangesAsync(cancellationToken);
        return profile;
    }

    public Task<SupplierProfile?> GetSupplierAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.SupplierProfiles.AsNoTracking().SingleOrDefaultAsync(profile => profile.Id == id, cancellationToken);

    public async Task<SupplierProfile> SaveSupplierAsync(Guid id, SupplierProfileInput input, CancellationToken cancellationToken)
    {
        SupplierProfile? profile = await dbContext.SupplierProfiles.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        DateTimeOffset now = timeProvider.GetUtcNow();
        bool creating = profile is null;
        if (profile is null)
        {
            profile = SupplierProfile.Create(id, input.TradingName, input.CompanyRegistrationNumber, input.TaxIdentifier, input.ContactPerson, input.ContactEmail, input.PhoneNumber, input.Website, input.Address, input.City, input.County, input.Latitude, input.Longitude, input.Description, now);
            dbContext.SupplierProfiles.Add(profile);
            dbContext.CompanyMemberships.Add(CompanyMembership.Create(id, profile.Id, CompanyMembershipRole.Owner, now));
        }
        else profile.Update(input.TradingName, input.CompanyRegistrationNumber, input.TaxIdentifier, input.ContactPerson, input.ContactEmail, input.PhoneNumber, input.Website, input.Address, input.City, input.County, input.Latitude, input.Longitude, input.Description, now);
        dbContext.AccountAuditEvents.Add(AccountAuditEvent.Create(id, id, AccountAuditEventType.ProfileSaved, "Profil de furnizor salvat.", now));
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (creating && IsUniqueViolation(exception))
        {
            // A concurrent first-time registration already created this legacy self-company
            // (its key is shared with the owner's user id). Treat the duplicate as idempotent
            // success instead of surfacing a PK_SupplierProfiles/PK_CompanyMemberships 500.
            dbContext.ChangeTracker.Clear();
            return await dbContext.SupplierProfiles.AsNoTracking().SingleAsync(item => item.Id == id, cancellationToken);
        }
        return profile;
    }

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is Npgsql.PostgresException { SqlState: "23505" };
}
