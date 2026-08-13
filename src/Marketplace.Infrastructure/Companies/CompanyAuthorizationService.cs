using Marketplace.Application.Companies;
using Marketplace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Infrastructure.Companies;

public sealed class CompanyAuthorizationService(MarketplaceDbContext dbContext) : ICompanyAuthorizationService
{
    public Task<bool> CanRepresentCompanyAsync(Guid userId, Guid companyId, CancellationToken cancellationToken) =>
        dbContext.CompanyMemberships.AsNoTracking().AnyAsync(
            membership => membership.UserId == userId
                && membership.CompanyId == companyId
                && membership.RevokedAt == null,
            cancellationToken);

    public async Task<bool> CanCreateLegacyCompanyAsync(Guid userId, Guid companyId, CancellationToken cancellationToken)
    {
        if (userId != companyId) return false;
        return !await dbContext.SupplierProfiles.AsNoTracking()
            .AnyAsync(profile => profile.Id == companyId, cancellationToken);
    }
}
