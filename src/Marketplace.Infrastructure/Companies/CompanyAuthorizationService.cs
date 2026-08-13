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

    public async Task<CompanyContextResolution> ResolveCompanyContextAsync(Guid userId, Guid? companyId, CancellationToken cancellationToken)
    {
        if (companyId.HasValue)
            return await CanRepresentCompanyAsync(userId, companyId.Value, cancellationToken)
                ? CompanyContextResolution.Resolved(companyId.Value)
                : CompanyContextResolution.Forbidden();

        Guid[] companyIds = await dbContext.CompanyMemberships.AsNoTracking()
            .Where(membership => membership.UserId == userId && membership.RevokedAt == null)
            .Select(membership => membership.CompanyId)
            .Take(2)
            .ToArrayAsync(cancellationToken);
        return companyIds.Length switch
        {
            0 => CompanyContextResolution.Unavailable(),
            1 => CompanyContextResolution.Resolved(companyIds[0]),
            _ => CompanyContextResolution.Ambiguous()
        };
    }
}
