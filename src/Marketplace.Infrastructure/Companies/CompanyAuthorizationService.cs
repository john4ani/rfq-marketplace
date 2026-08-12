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
}
