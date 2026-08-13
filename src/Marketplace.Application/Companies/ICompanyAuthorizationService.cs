namespace Marketplace.Application.Companies;

public interface ICompanyAuthorizationService
{
    Task<bool> CanRepresentCompanyAsync(Guid userId, Guid companyId, CancellationToken cancellationToken);
    Task<bool> CanCreateLegacyCompanyAsync(Guid userId, Guid companyId, CancellationToken cancellationToken);
    Task<CompanyContextResolution> ResolveCompanyContextAsync(Guid userId, Guid? companyId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<Guid>> GetActiveMemberUserIdsAsync(Guid companyId, CancellationToken cancellationToken);
}

public enum CompanyContextStatus { Resolved, Unavailable, Ambiguous, Forbidden }
public sealed record CompanyContextResolution(CompanyContextStatus Status, Guid? CompanyId)
{
    public static CompanyContextResolution Resolved(Guid companyId) => new(CompanyContextStatus.Resolved, companyId);
    public static CompanyContextResolution Unavailable() => new(CompanyContextStatus.Unavailable, null);
    public static CompanyContextResolution Ambiguous() => new(CompanyContextStatus.Ambiguous, null);
    public static CompanyContextResolution Forbidden() => new(CompanyContextStatus.Forbidden, null);
}
