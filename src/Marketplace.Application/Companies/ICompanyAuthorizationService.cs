namespace Marketplace.Application.Companies;

public interface ICompanyAuthorizationService
{
    Task<bool> CanRepresentCompanyAsync(Guid userId, Guid companyId, CancellationToken cancellationToken);
}
