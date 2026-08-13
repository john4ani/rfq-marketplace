namespace Marketplace.Application.Companies;

public interface ICompanyAuthorizationService
{
    Task<bool> CanRepresentCompanyAsync(Guid userId, Guid companyId, CancellationToken cancellationToken);
    Task<bool> CanCreateLegacyCompanyAsync(Guid userId, Guid companyId, CancellationToken cancellationToken);
}
