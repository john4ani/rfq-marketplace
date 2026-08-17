using System.Security.Cryptography;
using System.Text;
using Marketplace.Application.Categories;
using Marketplace.Application.SupplierDiscovery;
using Marketplace.Domain.Rfqs;
using Marketplace.Domain.Profiles;
using Marketplace.Domain.SupplierDiscovery;
using Marketplace.Domain.Suppliers;
using Marketplace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Infrastructure.SupplierDiscovery;

public sealed class SupplierDiscoveryService(
    MarketplaceDbContext dbContext,
    TimeProvider timeProvider) : ISupplierDiscoveryService
{
    public async Task<IReadOnlyCollection<DiscoveryOpportunity>> GetOpenOpportunitiesAsync(
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        var opportunities = await dbContext.Rfqs.AsNoTracking()
            .Where(rfq => (rfq.Status == RfqStatus.Published || rfq.Status == RfqStatus.ReceivingOffers)
                && rfq.QuotationDeadline > now)
            .OrderBy(rfq => rfq.QuotationDeadline)
            .Select(rfq => new
            {
                rfq.Id,
                rfq.Title,
                rfq.DeliveryLocality,
                rfq.County,
                rfq.QuotationDeadline
            })
            .ToArrayAsync(cancellationToken);
        Guid[] opportunityIds = opportunities.Select(opportunity => opportunity.Id).ToArray();
        var requiredCategories = await dbContext.ProcurementNeedCategories.AsNoTracking()
            .Where(category => opportunityIds.Contains(category.ProcurementNeedId))
            .OrderBy(category => category.SortOrder)
            .Select(category => new { category.ProcurementNeedId, category.CategoryId })
            .ToArrayAsync(cancellationToken);
        Dictionary<Guid, Guid[]> categoriesByRfq = requiredCategories
            .GroupBy(category => category.ProcurementNeedId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(category => category.CategoryId).Distinct().ToArray());
        return opportunities.Select(rfq => new DiscoveryOpportunity(
            rfq.Id,
            rfq.Title,
            rfq.DeliveryLocality,
            rfq.County,
            rfq.QuotationDeadline,
            categoriesByRfq[rfq.Id][0],
            categoriesByRfq[rfq.Id]))
            .ToArray();
    }

    public async Task<IReadOnlyCollection<SupplierCandidateView>> GetCandidatesAsync(CancellationToken cancellationToken)
    {
        SupplierCandidate[] candidates = await dbContext.SupplierCandidates.AsNoTracking().Include(candidate => candidate.Categories)
            .OrderBy(candidate => candidate.Status)
            .ThenByDescending(candidate => candidate.CreatedAt)
            .ToArrayAsync(cancellationToken);
        Guid[] candidateIds = candidates.Select(candidate => candidate.Id).ToArray();
        SupplierInvitation[] invitations = await dbContext.SupplierInvitations.AsNoTracking()
            .Where(invitation => candidateIds.Contains(invitation.CandidateId))
            .OrderByDescending(invitation => invitation.CreatedAt)
            .ToArrayAsync(cancellationToken);
        Guid[] categoryIds = candidates.SelectMany(candidate => candidate.CategoryIds).Distinct().ToArray();
        Dictionary<Guid, string> categories = await dbContext.Categories.AsNoTracking()
            .Where(category => categoryIds.Contains(category.Id))
            .ToDictionaryAsync(category => category.Id, category => category.Name, cancellationToken);
        Guid[] rfqIds = invitations.Select(invitation => invitation.RfqId).Distinct().ToArray();
        Dictionary<Guid, string> rfqTitles = await dbContext.Rfqs.AsNoTracking()
            .Where(rfq => rfqIds.Contains(rfq.Id))
            .ToDictionaryAsync(rfq => rfq.Id, rfq => rfq.Title, cancellationToken);
        DateTimeOffset now = timeProvider.GetUtcNow();

        return candidates.Select(candidate => ToView(
            candidate,
            categories,
            invitations.Where(invitation => invitation.CandidateId == candidate.Id)
                .Select(invitation => new SupplierInvitationSummary(
                    invitation.Id,
                    invitation.RfqId,
                    rfqTitles.GetValueOrDefault(invitation.RfqId, "Cerere indisponibilă"),
                    invitation.StatusAt(now),
                    invitation.CreatedAt,
                    invitation.ExpiresAt))
                .ToArray()))
            .ToArray();
    }

    public async Task<SupplierCandidateView> CreateCandidateAsync(
        Guid administratorId,
        SupplierCandidateInput input,
        CancellationToken cancellationToken)
    {
        Guid[] categoryIds = input.CategoryIds.Where(value => value != Guid.Empty).Distinct().ToArray();
        int validCategories = await dbContext.Categories.CountAsync(
            category => categoryIds.Contains(category.Id) && category.IsActive,
            cancellationToken);
        if (categoryIds.Length == 0 || validCategories != categoryIds.Length)
            throw new SupplierDiscoveryRuleException("Selectează cel puțin o categorie disponibilă.");

        DateTimeOffset now = timeProvider.GetUtcNow();
        SupplierCandidate candidate = SupplierCandidate.Create(
            Guid.NewGuid(), input.CompanyName, input.ContactEmail, input.PhoneNumber, input.Website,
            input.City, input.County, categoryIds, input.Source, input.Notes, administratorId, now);
        if (await dbContext.SupplierCandidates.AnyAsync(existing =>
                existing.NormalizedCompanyName == candidate.NormalizedCompanyName
                && existing.County == candidate.County,
                cancellationToken))
            throw new SupplierCandidateConflictException(
                "Există deja o firmă candidată cu această denumire în județul selectat.");
        string[] registeredCompanyNames = await dbContext.SupplierProfiles.AsNoTracking()
            .Where(existing => existing.County == candidate.County)
            .Select(existing => existing.TradingName)
            .ToArrayAsync(cancellationToken);
        if (registeredCompanyNames.Any(existing => string.Equals(
                existing.Trim(), candidate.CompanyName, StringComparison.OrdinalIgnoreCase)))
            throw new SupplierCandidateConflictException(
                "Firma este deja înregistrată în CompaSpot. Folosește potrivirea firmei înregistrate, fără a crea un candidat duplicat.");

        dbContext.SupplierCandidates.Add(candidate);
        dbContext.SupplierDiscoveryAuditEvents.Add(SupplierDiscoveryAuditEvent.Create(
            candidate.Id, administratorId, SupplierDiscoveryAuditEventType.CandidateCreated, now,
            details: $"Source={candidate.Source}"));
        await CreateMatchesForCandidateAsync(candidate, now, cancellationToken);
        await SaveChangesAsync(cancellationToken);
        Dictionary<Guid, string> categoryNames = await dbContext.Categories.Where(category => categoryIds.Contains(category.Id))
            .ToDictionaryAsync(category => category.Id, category => category.Name, cancellationToken);
        return ToView(candidate, categoryNames, []);
    }

    public async Task<SupplierCandidateView?> ReviewCandidateAsync(
        Guid administratorId,
        Guid candidateId,
        bool approved,
        string notes,
        CancellationToken cancellationToken)
    {
        SupplierCandidate? candidate = await dbContext.SupplierCandidates.Include(item => item.Categories)
            .SingleOrDefaultAsync(item => item.Id == candidateId, cancellationToken);
        if (candidate is null) return null;
        DateTimeOffset now = timeProvider.GetUtcNow();
        if (approved) candidate.ValidateRelevance(administratorId, notes, now);
        else candidate.Reject(administratorId, notes, now);
        dbContext.SupplierDiscoveryAuditEvents.Add(SupplierDiscoveryAuditEvent.Create(
            candidate.Id,
            administratorId,
            approved ? SupplierDiscoveryAuditEventType.RelevanceValidated : SupplierDiscoveryAuditEventType.CandidateRejected,
            now,
            details: notes));
        await SaveChangesAsync(cancellationToken);
        Dictionary<Guid, string> categoryNames = await dbContext.Categories
            .Where(category => candidate.CategoryIds.Contains(category.Id))
            .ToDictionaryAsync(category => category.Id, category => category.Name, cancellationToken);
        return ToView(candidate, categoryNames, []);
    }

    public async Task<SupplierInvitationCreated?> CreateInvitationAsync(
        Guid administratorId,
        Guid candidateId,
        Guid rfqId,
        CancellationToken cancellationToken)
    {
        SupplierCandidate? candidate = await dbContext.SupplierCandidates.Include(item => item.Categories)
            .SingleOrDefaultAsync(item => item.Id == candidateId, cancellationToken);
        if (candidate is null) return null;
        Rfq? rfq = await dbContext.Rfqs.Include(item => item.NeedCategories).Include(item => item.Items).ThenInclude(item => item.NeedCategory)
            .SingleOrDefaultAsync(item => item.Id == rfqId, cancellationToken);
        DateTimeOffset now = timeProvider.GetUtcNow();
        if (rfq is null || rfq.Status is not (RfqStatus.Published or RfqStatus.ReceivingOffers)
            || rfq.QuotationDeadline <= now)
            throw new SupplierDiscoveryRuleException("Invitația poate fi asociată doar unei cereri care primește oferte.");
        await EnsureCategoryRelevance(candidate, rfq, cancellationToken);

        OpportunityMatch? opportunityMatch = await dbContext.OpportunityMatches
            .SingleOrDefaultAsync(match => match.RfqId == rfqId && match.SupplierCandidateId == candidateId, cancellationToken);
        if (opportunityMatch is not null && opportunityMatch.Status != OpportunityMatchStatus.Approved)
            throw new SupplierDiscoveryRuleException(
                "Aprobă mai întâi această firmă pentru cererea selectată. Validarea este specifică oportunității și nu trimite nimic automat.");
        if (opportunityMatch is null && candidate.Status != SupplierCandidateStatus.EligibleForInvitation)
            throw new SupplierDiscoveryRuleException("Validează relevanța firmei înainte de a crea invitația.");

        SupplierInvitation[] previous = await dbContext.SupplierInvitations
            .Where(invitation => invitation.CandidateId == candidateId && invitation.RfqId == rfqId)
            .ToArrayAsync(cancellationToken);
        if (previous.Any(invitation => invitation.StatusAt(now) == SupplierInvitationStatus.Active))
            throw new SupplierCandidateConflictException("Există deja o invitație activă pentru această firmă și cerere.");

        string token = CreateToken();
        SupplierInvitation invitation = SupplierInvitation.Create(
            Guid.NewGuid(), candidateId, rfqId, Hash(token), administratorId, now, now.AddDays(14));
        dbContext.SupplierInvitations.Add(invitation);
        dbContext.SupplierDiscoveryAuditEvents.Add(SupplierDiscoveryAuditEvent.Create(
            candidate.Id, administratorId, SupplierDiscoveryAuditEventType.InvitationCreated, now,
            invitation.Id, rfq.Id, "Human-approved request-linked invitation"));
        await SaveChangesAsync(cancellationToken);
        return new(invitation.Id, token, invitation.ExpiresAt);
    }

    public async Task<SupplierInvitationView?> GetInvitationAsync(
        string token,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        SupplierInvitation? invitation = await dbContext.SupplierInvitations.AsNoTracking()
            .SingleOrDefaultAsync(item => item.TokenHash == Hash(token), cancellationToken);
        return invitation is null ? null : await ToInvitationView(invitation, cancellationToken);
    }

    public async Task<SupplierInvitationView?> ClaimAsync(
        Guid actorId,
        Guid companyId,
        string token,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        SupplierInvitation? invitation = await dbContext.SupplierInvitations
            .SingleOrDefaultAsync(item => item.TokenHash == Hash(token), cancellationToken);
        if (invitation is null) return null;
        SupplierCandidate candidate = await dbContext.SupplierCandidates.Include(item => item.Categories)
            .SingleAsync(item => item.Id == invitation.CandidateId, cancellationToken);
        if (!await dbContext.SupplierProfiles.AnyAsync(profile => profile.Id == companyId, cancellationToken))
            throw new SupplierDiscoveryRuleException("Completează profilul de furnizor înainte de a revendica firma.");
        if (candidate.ClaimedBySupplierId == actorId && invitation.ClaimedBySupplierId == actorId)
            return await ToInvitationView(invitation, cancellationToken);

        DateTimeOffset now = timeProvider.GetUtcNow();
        invitation.Claim(actorId, now);
        candidate.Claim(actorId, now);
        Guid[] existingCategoryIds = await dbContext.SupplierCategories
            .Where(category => category.SupplierId == companyId)
            .Select(category => category.CategoryId)
            .ToArrayAsync(cancellationToken);
        HashSet<Guid> existingCategories = existingCategoryIds.ToHashSet();
        dbContext.SupplierCategories.AddRange(candidate.CategoryIds
            .Where(categoryId => !existingCategories.Contains(categoryId))
            .Select(categoryId => SupplierCategory.Create(companyId, categoryId)));
        SupplierInvitation[] otherInvitations = await dbContext.SupplierInvitations
            .Where(item => item.CandidateId == candidate.Id && item.Id != invitation.Id)
            .ToArrayAsync(cancellationToken);
        foreach (SupplierInvitation other in otherInvitations)
            if (other.StatusAt(now) == SupplierInvitationStatus.Active)
                other.Revoke(now);
        dbContext.SupplierDiscoveryAuditEvents.Add(SupplierDiscoveryAuditEvent.Create(
            candidate.Id, actorId, SupplierDiscoveryAuditEventType.CompanyClaimed, now,
            invitation.Id, invitation.RfqId));
        await SaveChangesAsync(cancellationToken);
        return await ToInvitationView(invitation, cancellationToken);
    }

    public async Task<DiscoveryNeedReview?> GetNeedReviewAsync(Guid rfqId, CancellationToken cancellationToken)
    {
        var need = await dbContext.Rfqs.AsNoTracking().Where(r => r.Id == rfqId)
            .Select(r => new { r.Id, r.Title, r.DeliveryLocality, r.County, r.QuotationDeadline }).SingleOrDefaultAsync(cancellationToken);
        if (need is null) return null;
        OpportunityMatch[] matches = await dbContext.OpportunityMatches.AsNoTracking().Where(m => m.RfqId == rfqId).OrderBy(m => m.MatchedAt).ToArrayAsync(cancellationToken);
        return new(need.Id, need.Title, need.DeliveryLocality, need.County, need.QuotationDeadline,
            await ToOpportunityViewsAsync(matches.Where(m => m.SupplierProfileId != null).ToArray(), cancellationToken),
            await ToOpportunityViewsAsync(matches.Where(m => m.SupplierCandidateId != null).ToArray(), cancellationToken));
    }

    public async Task<IReadOnlyCollection<AcquisitionCandidate>> GetAcquisitionCandidatesAsync(string? county, Guid? categoryId, CancellationToken cancellationToken)
    {
        IQueryable<SupplierCandidate> query = dbContext.SupplierCandidates.AsNoTracking().Include(c => c.Categories).Where(c => c.Status != SupplierCandidateStatus.Claimed);
        if (!string.IsNullOrWhiteSpace(county)) query = query.Where(c => c.County == county.Trim());
        if (categoryId.HasValue) query = query.Where(c => c.Categories.Any(x => x.CategoryId == categoryId));
        SupplierCandidate[] candidates = await query.ToArrayAsync(cancellationToken);
        Guid[] ids = candidates.Select(c => c.Id).ToArray(); DateTimeOffset now = timeProvider.GetUtcNow();
        var rows = await dbContext.OpportunityMatches.AsNoTracking().Where(m => m.SupplierCandidateId != null && ids.Contains(m.SupplierCandidateId.Value))
            .Join(dbContext.Rfqs.AsNoTracking(), m => m.RfqId, r => r.Id, (m, r) => new { m, r.Status, r.QuotationDeadline }).ToArrayAsync(cancellationToken);
        Dictionary<Guid, string> names = await CategoryNamesAsync(candidates.SelectMany(c => c.CategoryIds), cancellationToken);
        Dictionary<Guid, int> invitations = await dbContext.SupplierInvitations.AsNoTracking().Where(i => ids.Contains(i.CandidateId)).GroupBy(i => i.CandidateId).ToDictionaryAsync(g => g.Key, g => g.Count(), cancellationToken);
        DateTimeOffset recentThreshold = now.AddDays(-30);
        return candidates.Select(c => { var values = rows.Where(x => x.m.SupplierCandidateId == c.Id).ToArray(); return new AcquisitionCandidate(c.Id,c.CompanyName,c.City,c.County,c.CategoryIds.Select(id=>new SupplierCandidateCategoryView(id,names.GetValueOrDefault(id,"Categorie indisponibilă"))).ToArray(),values.Length,values.Count(x=>(x.Status==RfqStatus.Published||x.Status==RfqStatus.ReceivingOffers)&&x.QuotationDeadline>now),values.Count(x=>x.m.Status==OpportunityMatchStatus.Approved),values.Count(x=>x.m.Status==OpportunityMatchStatus.Rejected),values.Select(x=>(DateTimeOffset?)x.m.MatchedAt).Max(),invitations.GetValueOrDefault(c.Id),false,values.Count(x=>x.m.MatchedAt>=recentThreshold)); }).OrderByDescending(x=>x.ActiveOpportunityCount).ThenByDescending(x=>x.RelevantOpportunityCount).ToArray();
    }

    public async Task<OpportunityMatchView?> ReviewOpportunityMatchAsync(Guid administratorId, Guid matchId, bool approved, string? note, CancellationToken cancellationToken)
    {
        OpportunityMatch? match = await dbContext.OpportunityMatches.SingleOrDefaultAsync(m => m.Id == matchId, cancellationToken);
        if (match is null) return null;
        match.Review(administratorId, approved, note, timeProvider.GetUtcNow());
        await SaveChangesAsync(cancellationToken);
        return (await ToOpportunityViewsAsync([match], cancellationToken)).Single();
    }

    private async Task<IReadOnlyCollection<OpportunityMatchView>> ToOpportunityViewsAsync(OpportunityMatch[] matches, CancellationToken cancellationToken)
    {
        if (matches.Length == 0) return [];
        Guid[] candidateIds = matches.Where(m=>m.SupplierCandidateId!=null).Select(m=>m.SupplierCandidateId!.Value).ToArray();
        Guid[] companyIds = matches.Where(m=>m.SupplierProfileId!=null).Select(m=>m.SupplierProfileId!.Value).ToArray();
        Dictionary<Guid,SupplierCandidate> candidates = await dbContext.SupplierCandidates.AsNoTracking().Where(c=>candidateIds.Contains(c.Id)).ToDictionaryAsync(c=>c.Id,c=>c,cancellationToken);
        Dictionary<Guid,SupplierProfile> companies = await dbContext.SupplierProfiles.AsNoTracking().Where(c=>companyIds.Contains(c.Id)).ToDictionaryAsync(c=>c.Id,c=>c,cancellationToken);
        var history = await dbContext.OpportunityMatches.AsNoTracking().Where(m=>m.SupplierCandidateId!=null&&candidateIds.Contains(m.SupplierCandidateId.Value)).ToArrayAsync(cancellationToken);
        var invitationCounts = await dbContext.SupplierInvitations.AsNoTracking()
            .Where(invitation => candidateIds.Contains(invitation.CandidateId))
            .GroupBy(invitation => new { invitation.CandidateId, invitation.RfqId })
            .ToDictionaryAsync(group => (group.Key.CandidateId, group.Key.RfqId), group => group.Count(), cancellationToken);
        DateTimeOffset now=timeProvider.GetUtcNow(); var activeIds=await dbContext.Rfqs.AsNoTracking().Where(r=>(r.Status==RfqStatus.Published||r.Status==RfqStatus.ReceivingOffers)&&r.QuotationDeadline>now).Select(r=>r.Id).ToArrayAsync(cancellationToken);
        DateTimeOffset recentThreshold = now.AddDays(-30);
        return matches.Select(m=>{var c=m.SupplierCandidateId.HasValue?candidates.GetValueOrDefault(m.SupplierCandidateId.Value):null;var p=m.SupplierProfileId.HasValue?companies.GetValueOrDefault(m.SupplierProfileId.Value):null;var h=c is null?[]:history.Where(x=>x.SupplierCandidateId==c.Id).ToArray();return new OpportunityMatchView(m.Id,m.RfqId,m.SupplierProfileId,m.SupplierCandidateId,c?.CompanyName??p?.TradingName??"Firmă indisponibilă",c?.City??p?.City,c?.County??p?.County,m.MatchReason,m.Status,m.MatchedAt,m.AdminReviewNote,h.Length,h.Count(x=>activeIds.Contains(x.RfqId)),h.Count(x=>x.Status==OpportunityMatchStatus.Approved),h.Count(x=>x.Status==OpportunityMatchStatus.Rejected),h.Select(x=>(DateTimeOffset?)x.MatchedAt).Max(),c?.Status==SupplierCandidateStatus.Claimed,c is null?0:invitationCounts.GetValueOrDefault((c.Id,m.RfqId)),h.Count(x=>x.MatchedAt>=recentThreshold),c?.ContactEmail,c?.PhoneNumber,c?.Website);}).ToArray();
    }

    private async Task<Dictionary<Guid,string>> CategoryNamesAsync(IEnumerable<Guid> ids, CancellationToken ct) => await dbContext.Categories.AsNoTracking().Where(c=>ids.Contains(c.Id)).ToDictionaryAsync(c=>c.Id,c=>c.Name,ct);

    private async Task EnsureCategoryRelevance(
        SupplierCandidate candidate,
        Rfq rfq,
        CancellationToken cancellationToken)
    {
        Dictionary<Guid, Guid?> parents = await dbContext.Categories.AsNoTracking()
            .Where(category => category.IsActive)
            .ToDictionaryAsync(category => category.Id, category => category.ParentId, cancellationToken);
        HashSet<Guid> candidateCategories = CategoryCompatibilityPolicy.Expand(candidate.CategoryIds, parents);
        HashSet<Guid> requiredCategories = rfq.RequiredCategoryIds.ToHashSet();
        if (!requiredCategories.Any(candidateCategories.Contains))
            throw new SupplierDiscoveryRuleException(
                "Categoria firmei nu corespunde niciunei poziții din cererea selectată.");
    }

    private async Task CreateMatchesForCandidateAsync(
        SupplierCandidate candidate,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var openNeeds = await dbContext.Rfqs.AsNoTracking()
            .Where(rfq => (rfq.Status == RfqStatus.Published || rfq.Status == RfqStatus.ReceivingOffers)
                && rfq.QuotationDeadline > now && rfq.County == candidate.County)
            .Select(rfq => new { rfq.Id, rfq.County })
            .ToArrayAsync(cancellationToken);
        if (openNeeds.Length == 0) return;

        Guid[] needIds = openNeeds.Select(need => need.Id).ToArray();
        var needCategories = await dbContext.ProcurementNeedCategories.AsNoTracking()
            .Where(category => needIds.Contains(category.ProcurementNeedId))
            .Select(category => new { category.ProcurementNeedId, category.CategoryId })
            .ToArrayAsync(cancellationToken);
        Dictionary<Guid, Guid?> parents = await dbContext.Categories.AsNoTracking()
            .Where(category => category.IsActive)
            .ToDictionaryAsync(category => category.Id, category => category.ParentId, cancellationToken);
        Dictionary<Guid, string> categoryNames = await dbContext.Categories.AsNoTracking()
            .ToDictionaryAsync(category => category.Id, category => category.Name, cancellationToken);
        HashSet<Guid> compatibleCategories = CategoryCompatibilityPolicy.Expand(candidate.CategoryIds, parents);

        foreach (var need in openNeeds)
        {
            Guid? categoryId = needCategories.Where(category => category.ProcurementNeedId == need.Id
                    && compatibleCategories.Contains(category.CategoryId))
                .Select(category => (Guid?)category.CategoryId).FirstOrDefault();
            if (!categoryId.HasValue) continue;
            dbContext.OpportunityMatches.Add(OpportunityMatch.ForCandidate(
                need.Id,
                candidate.Id,
                OpportunityMatchSource.CandidateDiscovery,
                $"Categorie compatibilă: {categoryNames.GetValueOrDefault(categoryId.Value, "categorie selectată")} · județ: {need.County}",
                now));
        }
    }

    private async Task<SupplierInvitationView> ToInvitationView(
        SupplierInvitation invitation,
        CancellationToken cancellationToken)
    {
        SupplierCandidate candidate = await dbContext.SupplierCandidates.AsNoTracking()
            .SingleAsync(item => item.Id == invitation.CandidateId, cancellationToken);
        var rfq = await dbContext.Rfqs.AsNoTracking()
            .Where(item => item.Id == invitation.RfqId)
            .Select(item => new
            {
                item.Id,
                item.Title,
                item.Description,
                item.DeliveryLocality,
                item.County,
                item.QuotationDeadline,
                RequirementCount = item.Items.Count
            })
            .SingleAsync(cancellationToken);
        SupplierInvitationStatus status = invitation.StatusAt(timeProvider.GetUtcNow());
        return new(
            invitation.Id,
            candidate.Id,
            candidate.CompanyName,
            "Firmă descoperită extern · date nerevendicate și neverificate de companie",
            status,
            invitation.ExpiresAt,
            rfq.Id,
            rfq.Title,
            rfq.Description,
            rfq.DeliveryLocality,
            rfq.County,
            rfq.QuotationDeadline,
            rfq.RequirementCount,
            status == SupplierInvitationStatus.Claimed);
    }

    private static SupplierCandidateView ToView(
        SupplierCandidate candidate,
        IReadOnlyDictionary<Guid, string> categoryNames,
        IReadOnlyCollection<SupplierInvitationSummary> invitations) =>
        new(
            candidate.Id,
            candidate.CompanyName,
            candidate.ContactEmail,
            candidate.PhoneNumber,
            candidate.Website,
            candidate.City,
            candidate.County,
            candidate.CategoryIds.Select(categoryId => new SupplierCandidateCategoryView(
                categoryId,
                categoryNames.GetValueOrDefault(categoryId, "Categorie indisponibilă"))).ToArray(),
            candidate.Source,
            candidate.Notes,
            candidate.Status,
            candidate.CreatedAt,
            candidate.ReviewNotes,
            candidate.ClaimedBySupplierId,
            invitations);

    private async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new SupplierDiscoveryConcurrencyException(
                "Datele au fost modificate între timp. Reîncarcă pagina și încearcă din nou.",
                exception);
        }
        catch (DbUpdateException exception) when (exception.InnerException?.Message.Contains(
            "IX_SupplierCandidates_NormalizedCompanyName_County",
            StringComparison.Ordinal) == true)
        {
            throw new SupplierCandidateConflictException(
                "Există deja o firmă candidată cu această denumire în județul selectat.");
        }
    }

    private static string CreateToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token.Trim())));
}
