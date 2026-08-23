using Marketplace.Application.Companies;
using Marketplace.Domain.Profiles;
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
        return companies.Select(company => Entry(company.Id, company.TradingName, "Registered", true, company.City, company.County, company.ContactEmail, company.PhoneNumber, company.Website, company.TaxIdentifier, companyCategories.Where(item => item.SupplierId == company.Id).Select(item => categoryNames.GetValueOrDefault(item.CategoryId, "Categorie indisponibilă")), matches.Where(match => match.SupplierProfileId == company.Id), rfqs, company.CreatedAt, null, now, company.IsActive))
            .Concat(candidates.Select(candidate => Entry(candidate.Id, candidate.CompanyName, "Discovered", candidate.Status == SupplierCandidateStatus.Claimed, candidate.City, candidate.County, candidate.ContactEmail, candidate.PhoneNumber, candidate.Website, null, candidate.CategoryIds.Select(id => categoryNames.GetValueOrDefault(id, "Categorie indisponibilă")), matches.Where(match => match.SupplierCandidateId == candidate.Id), rfqs, candidate.CreatedAt, suppressions.Contains(candidate.Id) ? "Nu mai contacta" : candidate.Status == SupplierCandidateStatus.Claimed ? "Revendicată" : "Nerevendicată", now, false)))
            .OrderBy(entry => entry.Name).ToArray();
    }

    public async Task<CompanyRegistryEntry?> UpdateDiscoveredAsync(Guid candidateId, DiscoveredCompanyUpdate input, CancellationToken cancellationToken)
    {
        SupplierCandidate? candidate = await dbContext.SupplierCandidates.Include(item => item.Categories)
            .SingleOrDefaultAsync(item => item.Id == candidateId, cancellationToken);
        if (candidate is null) return null;

        Guid[] categoryIds = input.CategoryIds.Where(item => item != Guid.Empty).Distinct().ToArray();
        int validCategoryCount = await dbContext.Categories.CountAsync(
            item => categoryIds.Contains(item.Id) && item.IsActive, cancellationToken);
        if (categoryIds.Length == 0 || validCategoryCount != categoryIds.Length)
            throw new InvalidOperationException("Selectează cel puțin o categorie disponibilă.");

        string normalizedName = NormalizeCompanyName(input.CompanyName);
        bool duplicate = await dbContext.SupplierCandidates.AnyAsync(item =>
            item.Id != candidate.Id && item.NormalizedCompanyName == normalizedName && item.County == input.County.Trim(), cancellationToken);
        if (duplicate)
            throw new InvalidOperationException("Există deja o firmă descoperită cu această denumire în județul selectat.");

        candidate.UpdateDetails(input.CompanyName, input.Email, input.Phone, input.Website, input.Locality, input.County, categoryIds);
        await dbContext.SaveChangesAsync(cancellationToken);
        return (await GetAsync(cancellationToken)).SingleOrDefault(item => item.Id == candidateId && item.Kind == "Discovered");
    }

    public async Task<bool> DeleteDiscoveredAsync(Guid candidateId, CancellationToken cancellationToken)
    {
        SupplierCandidate? candidate = await dbContext.SupplierCandidates.SingleOrDefaultAsync(item => item.Id == candidateId, cancellationToken);
        if (candidate is null) return false;
        if (candidate.Status == SupplierCandidateStatus.Claimed)
            throw new InvalidOperationException("Firma revendicată nu poate fi ștearsă din registrul de firme descoperite.");
        bool hasContactHistory = await dbContext.SupplierInvitations.AnyAsync(item => item.CandidateId == candidateId, cancellationToken)
            || await dbContext.SupplierOutreachContacts.AnyAsync(item => item.CandidateId == candidateId, cancellationToken);
        if (hasContactHistory)
            throw new InvalidOperationException("Firma are invitații sau contactări înregistrate. Păstrăm istoricul; elimină potrivirea din contact pentru cererea relevantă.");

        await dbContext.OpportunityMatches.Where(item => item.SupplierCandidateId == candidateId).ExecuteDeleteAsync(cancellationToken);
        dbContext.SupplierCandidates.Remove(candidate);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<CompanyRegistryEntry?> SetRegisteredActiveAsync(Guid companyId, bool isActive, CancellationToken cancellationToken)
    {
        SupplierProfile? company = await dbContext.SupplierProfiles.SingleOrDefaultAsync(item => item.Id == companyId, cancellationToken);
        if (company is null) return null;
        company.SetActive(isActive, timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
        return (await GetAsync(cancellationToken)).SingleOrDefault(item => item.Id == companyId && item.Kind == "Registered");
    }

    private static CompanyRegistryEntry Entry(Guid id,string name,string kind,bool claimed,string locality,string county,string? email,string? phone,string? website,string? taxIdentifier,IEnumerable<string> categories,IEnumerable<OpportunityMatch> matches,Dictionary<Guid,Rfq> rfqs,DateTimeOffset createdAt,string? contactState,DateTimeOffset now,bool isActive)
    {
        var values=matches.ToArray();
        return new(id,name,kind,claimed,locality,county,email,phone,website,taxIdentifier,categories.Distinct().Order().ToArray(),values.Length,values.Count(match=>rfqs.TryGetValue(match.RfqId,out Rfq? rfq)&&(rfq.Status is RfqStatus.Published or RfqStatus.ReceivingOffers)&&rfq.QuotationDeadline>now),values.Count(match=>match.Status==OpportunityMatchStatus.Approved),values.Select(match=>(DateTimeOffset?)match.MatchedAt).Max(),createdAt,contactState,isActive);
    }

    private static string NormalizeCompanyName(string value) => string.Join(' ', value.Trim().ToUpperInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
}
