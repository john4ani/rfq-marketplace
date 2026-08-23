namespace Marketplace.Application.Companies;

public interface ICompanyRegistryService
{
    Task<IReadOnlyCollection<CompanyRegistryEntry>> GetAsync(CancellationToken cancellationToken);
    Task<CompanyRegistryEntry?> UpdateDiscoveredAsync(Guid candidateId, DiscoveredCompanyUpdate input, CancellationToken cancellationToken);
    Task<bool> DeleteDiscoveredAsync(Guid candidateId, CancellationToken cancellationToken);
    Task<CompanyRegistryEntry?> SetRegisteredActiveAsync(Guid companyId, bool isActive, CancellationToken cancellationToken);
}

public sealed record DiscoveredCompanyUpdate(
    string CompanyName,
    string Locality,
    string County,
    string? Email,
    string? Phone,
    string? Website,
    IReadOnlyCollection<Guid> CategoryIds);

public sealed record CompanyRegistryEntry(
    Guid Id,
    string Name,
    string Kind,
    bool Claimed,
    string Locality,
    string County,
    string? Email,
    string? Phone,
    string? Website,
    string? TaxIdentifier,
    IReadOnlyCollection<string> Categories,
    int RelevantOpportunities,
    int ActiveOpportunities,
    int ApprovedMatches,
    DateTimeOffset? LastRelevantOpportunity,
    DateTimeOffset CreatedAt,
    string? ContactState,
    bool IsActive);
