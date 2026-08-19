namespace Marketplace.Application.Companies;

public interface ICompanyRegistryService
{
    Task<IReadOnlyCollection<CompanyRegistryEntry>> GetAsync(CancellationToken cancellationToken);
}

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
    string? ContactState);
