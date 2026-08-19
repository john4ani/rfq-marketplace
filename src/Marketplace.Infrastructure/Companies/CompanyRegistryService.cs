using Marketplace.Application.Companies;
using Marketplace.Domain.Rfqs;
using Marketplace.Domain.SupplierDiscovery;
using Marketplace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Infrastructure.Companies;

public sealed class CompanyRegistryService(MarketplaceDbContext dbContext, TimeProvider timeProvider) : ICompanyRegistryService
{
    public async Task<IReadOnlyCollection<CompanyRegistryEntry>> GetAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        var companies = await dbContext.SupplierProfiles.AsNoTracking().ToArrayAsync(cancellationToken);
        var candidates = await dbContext.SupplierCandidates.AsNoTracking().Include(candidate => candidate.Categories).ToArrayAsync(cancellationToken);
        Guid[] companyIds = companies.Select(company => company.Id).ToArray();
        Guid[] candidateIds = candidates.Select(candidate => candidate.Id).ToArray();
        var categoryNames = await dbContext.Categories.AsNoTracking().ToDictionaryAsync(category => category.Id, category => category.Name, cancellationToken);
        var companyCategories = await dbContext.SupplierCategories.AsNoTracking().Where(item => companyIds.Contains(item.SupplierId)).ToArrayAsync(cancellationToken);
        var matches = await dbContext.OpportunityMatches.AsNoTracking().Where(match => (match.SupplierProfileId != null && companyIds.Contains(match.SupplierProfileId.Value)) || (match.SupplierCandidateId != null && candidateIds.Contains(match.SupplierCandidateId.Value))).ToArrayAsync(cancellationToken);
        Guid[] matchedNeedIds = matches.Select(match => match.RfqId).Distinct().ToArray();
        var rfqs = await dbContext.Rfqs.AsNoTracking().Where(rfq => matchedNeedIds.Contains(rfq.Id)).ToDictionaryAsync(rfq => rfq.Id, cancellationToken);
        var suppressions = await dbContext.SupplierContactSuppressions.AsNoTracking().Where(item => candidateIds.Contains(item.CandidateId)).Select(item => item.CandidateId).ToArrayAsync(cancellationToken);
        return companies.Select(company => Entry(company.Id, company.TradingName, "Registered", true, company.City, company.County, company.ContactEmail, company.PhoneNumber, company.Website, company.TaxIdentifier, companyCategories.Where(item => item.SupplierId == company.Id).Select(item => categoryNames.GetValueOrDefault(item.CategoryId, "Categorie indisponibilă")), matches.Where(match => match.SupplierProfileId == company.Id), rfqs, company.CreatedAt, null, now))
            .Concat(candidates.Select(candidate => Entry(candidate.Id, candidate.CompanyName, "Discovered", candidate.Status == SupplierCandidateStatus.Claimed, candidate.City, candidate.County, candidate.ContactEmail, candidate.PhoneNumber, candidate.Website, null, candidate.CategoryIds.Select(id => categoryNames.GetValueOrDefault(id, "Categorie indisponibilă")), matches.Where(match => match.SupplierCandidateId == candidate.Id), rfqs, candidate.CreatedAt, suppressions.Contains(candidate.Id) ? "Nu mai contacta" : candidate.Status == SupplierCandidateStatus.Claimed ? "Revendicată" : "Nerevendicată", now)))
            .OrderBy(entry => entry.Name).ToArray();
    }

    private static CompanyRegistryEntry Entry(Guid id,string name,string kind,bool claimed,string locality,string county,string? email,string? phone,string? website,string? taxIdentifier,IEnumerable<string> categories,IEnumerable<OpportunityMatch> matches,Dictionary<Guid,Rfq> rfqs,DateTimeOffset createdAt,string? contactState,DateTimeOffset now)
    {
        var values=matches.ToArray();
        return new(id,name,kind,claimed,locality,county,email,phone,website,taxIdentifier,categories.Distinct().Order().ToArray(),values.Length,values.Count(match=>rfqs.TryGetValue(match.RfqId,out Rfq? rfq)&&(rfq.Status is RfqStatus.Published or RfqStatus.ReceivingOffers)&&rfq.QuotationDeadline>now),values.Count(match=>match.Status==OpportunityMatchStatus.Approved),values.Select(match=>(DateTimeOffset?)match.MatchedAt).Max(),createdAt,contactState);
    }
}
