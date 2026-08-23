using Marketplace.Application.Categories;
using Marketplace.Application.Companies;
using Marketplace.Application.Rfqs;
using Marketplace.Application.Operations;
using Marketplace.Application.Profiles;
using Marketplace.Domain.Offers;
using Marketplace.Domain.Notifications;
using Marketplace.Domain.Rfqs;
using Marketplace.Domain.SupplierDiscovery;
using Marketplace.Infrastructure.Persistence;
using Marketplace.Infrastructure.Profiles;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Infrastructure.Rfqs;

public sealed class RfqService(
    MarketplaceDbContext dbContext,
    ICompanyAuthorizationService companyAuthorizationService,
    ILocationGeocoder locationGeocoder,
    TimeProvider timeProvider,
    IProcurementNeedAuditTrail auditTrail,
    IProductEventService productEvents) : IRfqService
{
    public async Task<IReadOnlyCollection<Rfq>> GetMineAsync(Guid buyerId, CancellationToken cancellationToken) =>
        await dbContext.Rfqs.AsNoTracking().Include(rfq => rfq.NeedCategories)
            .Include(rfq => rfq.Items).Where(rfq => rfq.BuyerId == buyerId)
            .OrderByDescending(rfq => rfq.UpdatedAt).ToListAsync(cancellationToken);

    public Task<Rfq?> GetAsync(Guid buyerId, Guid rfqId, CancellationToken cancellationToken) =>
        dbContext.Rfqs.AsNoTracking().Include(rfq => rfq.NeedCategories)
            .Include(rfq => rfq.Items)
            .SingleOrDefaultAsync(rfq => rfq.Id == rfqId && rfq.BuyerId == buyerId, cancellationToken);

    public async Task<IReadOnlyCollection<ProcurementNeedAuditEvent>?> GetHistoryAsync(
        Guid buyerId, Guid rfqId, CancellationToken cancellationToken)
    {
        if (!await dbContext.Rfqs.AsNoTracking()
                .AnyAsync(rfq => rfq.Id == rfqId && rfq.BuyerId == buyerId, cancellationToken))
            return null;

        return await dbContext.ProcurementNeedAuditEvents.AsNoTracking()
            .Where(auditEvent => auditEvent.ProcurementNeedId == rfqId)
            .OrderBy(auditEvent => auditEvent.OccurredAt)
            .ThenBy(auditEvent => auditEvent.Id)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<Rfq> CreateAsync(Guid buyerId, RfqDraft draft, CancellationToken cancellationToken)
    {
        draft = await ResolveLocationAsync(buyerId, draft, cancellationToken);
        await ValidateReferences(buyerId, draft, cancellationToken);
        DateTimeOffset now = timeProvider.GetUtcNow();
        Rfq rfq = Rfq.Create(Guid.NewGuid(), buyerId, draft, now);
        dbContext.Rfqs.Add(rfq);
        auditTrail.Record(rfq.Id, buyerId, ProcurementNeedAuditEventType.Created,
            ProcurementNeedStatus.Draft, rfq.NeedStatus, null, now);
        productEvents.Record("ProcurementNeedCreated", "ProcurementNeed", rfq.Id, buyerId, null, now);
        await dbContext.SaveChangesAsync(cancellationToken);
        return rfq;
    }

    public async Task<Rfq?> UpdateAsync(Guid buyerId, Guid rfqId, RfqDraft draft, CancellationToken cancellationToken)
    {
        draft = await ResolveLocationAsync(buyerId, draft, cancellationToken);
        await ValidateReferences(buyerId, draft, cancellationToken);
        Rfq? rfq = await Find(buyerId, rfqId, cancellationToken);
        if (rfq is null) return null;
        DateTimeOffset now = timeProvider.GetUtcNow();
        ProcurementNeedStatus previousStatus = rfq.NeedStatus;
        rfq.Update(draft, now);
        auditTrail.Record(rfq.Id, buyerId, ProcurementNeedAuditEventType.Updated,
            previousStatus, rfq.NeedStatus, null, now);
        await SaveRfqChangesAsync(cancellationToken);
        return rfq;
    }

    public async Task<Rfq?> PublishAsync(Guid buyerId, Guid rfqId, CancellationToken cancellationToken)
    {
        Rfq? rfq = await Find(buyerId, rfqId, cancellationToken);
        if (rfq is null) return null;
        DateTimeOffset now = timeProvider.GetUtcNow();
        ProcurementNeedStatus previousStatus = rfq.NeedStatus;
        rfq.Publish(now);
        auditTrail.Record(rfq.Id, buyerId, ProcurementNeedAuditEventType.Published,
            previousStatus, rfq.NeedStatus, null, now);
        productEvents.Record("ProcurementNeedPublished", "ProcurementNeed", rfq.Id, buyerId, null, now);
        await NotifyMatchingSuppliersAsync(rfq, now, cancellationToken);
        await SaveRfqChangesAsync(cancellationToken);
        return rfq;
    }

    public async Task<Rfq?> CancelAsync(Guid buyerId, Guid rfqId, CancellationToken cancellationToken)
    {
        Rfq? rfq = await Find(buyerId, rfqId, cancellationToken);
        if (rfq is null) return null;
        DateTimeOffset now = timeProvider.GetUtcNow();
        ProcurementNeedStatus previousStatus = rfq.NeedStatus;
        rfq.Cancel(now);
        await RejectOpenOffers(rfqId, now, cancellationToken);
        auditTrail.Record(rfq.Id, buyerId, ProcurementNeedAuditEventType.Cancelled,
            previousStatus, rfq.NeedStatus, null, now);
        auditTrail.Record(rfq.Id, buyerId, ProcurementNeedAuditEventType.OutcomeRecorded,
            previousStatus, rfq.NeedStatus, $"Outcome={rfq.Outcome}", now.Add(TimeSpan.FromMicroseconds(1)));
        await SaveRfqChangesAsync(cancellationToken);
        return rfq;
    }

    public async Task<bool> DeleteDraftAsync(Guid buyerId, Guid rfqId, CancellationToken cancellationToken)
    {
        Rfq? rfq = await dbContext.Rfqs
            .SingleOrDefaultAsync(item => item.Id == rfqId && item.BuyerId == buyerId, cancellationToken);
        if (rfq is null) return false;
        if (rfq.Status != RfqStatus.Draft)
            throw new RfqRuleException("Doar o cerere în starea Ciornă poate fi ștearsă.");
        if (await dbContext.Offers.AnyAsync(offer => offer.RfqId == rfqId, cancellationToken))
            throw new RfqRuleException("Cererea are oferte asociate și nu poate fi ștearsă.");
        DateTimeOffset now = timeProvider.GetUtcNow();
        productEvents.Record("ProcurementNeedDeleted", "ProcurementNeed", rfq.Id, buyerId, null, now);
        // Delete children explicitly in dependency order: RfqItem references NeedCategory with DeleteBehavior.Restrict.
        await dbContext.ProcurementNeedAuditEvents.Where(auditEvent => auditEvent.ProcurementNeedId == rfqId)
            .ExecuteDeleteAsync(cancellationToken);
        await dbContext.RfqItems.Where(item => item.RfqId == rfqId).ExecuteDeleteAsync(cancellationToken);
        await dbContext.ProcurementNeedCategories.Where(category => category.ProcurementNeedId == rfqId)
            .ExecuteDeleteAsync(cancellationToken);
        dbContext.Rfqs.Remove(rfq);
        await SaveRfqChangesAsync(cancellationToken);
        return true;
    }

    public async Task<Rfq?> RecordOutcomeAsync(
        Guid buyerId,
        Guid rfqId,
        ProcurementOutcome outcome,
        string? reason,
        CancellationToken cancellationToken)
    {
        Rfq? rfq = await Find(buyerId, rfqId, cancellationToken);
        if (rfq is null) return null;
        if (outcome == ProcurementOutcome.PurchasedFromParticipant
            && !await dbContext.Offers.AnyAsync(offer => offer.RfqId == rfqId
                && (offer.Status == OfferStatus.Submitted || offer.Status == OfferStatus.Revised), cancellationToken))
            throw new RfqRuleException("Acest rezultat este disponibil doar când cererea a primit cel puțin o ofertă.");

        DateTimeOffset now = timeProvider.GetUtcNow();
        ProcurementNeedStatus previousStatus = rfq.NeedStatus;
        rfq.RecordOutcome(outcome, reason, now);
        await RejectOpenOffers(rfqId, now, cancellationToken);
        string details = string.IsNullOrWhiteSpace(rfq.OutcomeReason)
            ? $"Outcome={rfq.Outcome}"
            : $"Outcome={rfq.Outcome}; Reason={rfq.OutcomeReason}";
        auditTrail.Record(rfq.Id, buyerId, ProcurementNeedAuditEventType.OutcomeRecorded,
            previousStatus, rfq.NeedStatus, details, now);
        productEvents.Record("ProcurementOutcomeRecorded", "ProcurementNeed", rfq.Id, buyerId, $"Outcome={rfq.Outcome}", now);
        await SaveRfqChangesAsync(cancellationToken);
        return rfq;
    }

    public async Task<Rfq?> ExtendQuotationDeadlineAsync(
        Guid buyerId, Guid rfqId, DateTimeOffset newDeadline, CancellationToken cancellationToken)
    {
        Rfq? rfq = await Find(buyerId, rfqId, cancellationToken);
        if (rfq is null) return null;
        DateTimeOffset now = timeProvider.GetUtcNow();
        ProcurementNeedStatus previousStatus = rfq.NeedStatus;
        rfq.ExtendQuotationDeadline(newDeadline, now);
        auditTrail.Record(rfq.Id, buyerId, ProcurementNeedAuditEventType.DeadlineExtended,
            previousStatus, rfq.NeedStatus, null, now);
        await SaveRfqChangesAsync(cancellationToken);
        return rfq;
    }

    public async Task<IReadOnlyCollection<OpenRfq>> GetOpenAsync(
        Guid supplierId, Guid actorId, string? search, Guid? categoryId, CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        Guid[] manuallyMatchedRequestIds = await dbContext.ConciergeMatchOverrides.AsNoTracking()
            .Where(item => item.SupplierId == supplierId).Select(item => item.RfqId).ToArrayAsync(cancellationToken);
        // A registered-company OpportunityMatch is persisted matching history, not merely an
        // explanation. Preserve access to that exact opportunity even after category-model
        // consolidation; new matching still uses the need's one primary category below.
        Guid[] opportunityMatchedRequestIds = await dbContext.OpportunityMatches.AsNoTracking()
            .Where(item => item.SupplierProfileId == supplierId)
            .Select(item => item.RfqId)
            .ToArrayAsync(cancellationToken);
        HashSet<Guid> explicitlyMatchedRequestIds = manuallyMatchedRequestIds
            .Concat(opportunityMatchedRequestIds).ToHashSet();
        Guid[] supplierCategoryIds = await dbContext.SupplierCategories.AsNoTracking()
            .Where(selection => selection.SupplierId == supplierId)
            .Select(selection => selection.CategoryId)
            .ToArrayAsync(cancellationToken);
        if (supplierCategoryIds.Length == 0 && explicitlyMatchedRequestIds.Count == 0) return [];
        Dictionary<Guid, Guid?> categoryParents = await dbContext.Categories.AsNoTracking()
            .Where(category => category.IsActive)
            .ToDictionaryAsync(category => category.Id, category => category.ParentId, cancellationToken);
        HashSet<Guid> compatibleSupplierCategories = CategoryCompatibilityPolicy.Expand(supplierCategoryIds, categoryParents);
        Guid[] compatibleSupplierCategoryIds = compatibleSupplierCategories.ToArray();

        var supplierLocation = await dbContext.SupplierProfiles.AsNoTracking()
            .Where(profile => profile.Id == supplierId && profile.IsActive)
            .Select(profile => new { profile.Latitude, profile.Longitude, profile.MarketRadiusKm })
            .SingleOrDefaultAsync(cancellationToken);
        if (supplierLocation?.Latitude is null || supplierLocation.Longitude is null) return [];

        IQueryable<Rfq> query = dbContext.Rfqs.AsNoTracking().Include(rfq => rfq.NeedCategories)
            .Include(rfq => rfq.Items)
            .Where(rfq => (rfq.Status == RfqStatus.Published || rfq.Status == RfqStatus.ReceivingOffers)
                && rfq.QuotationDeadline > now
                && rfq.BuyerId != actorId
                && (explicitlyMatchedRequestIds.Contains(rfq.Id)
                    || rfq.NeedCategories.Any(category => compatibleSupplierCategoryIds.Contains(category.CategoryId))));
        if (categoryId.HasValue)
        {
            Guid[] compatibleFilterCategoryIds = CategoryCompatibilityPolicy.Expand([categoryId.Value], categoryParents).ToArray();
            query = query.Where(rfq => rfq.NeedCategories.Any(category => compatibleFilterCategoryIds.Contains(category.CategoryId)));
        }

        Rfq[] rfqs = await query.OrderBy(rfq => rfq.QuotationDeadline).ThenByDescending(rfq => rfq.PublishedAt)
            .ToArrayAsync(cancellationToken);
        rfqs = rfqs.Where(rfq => rfq.Latitude.HasValue && rfq.Longitude.HasValue
            && GeographicDistance.Kilometers(supplierLocation.Latitude.Value, supplierLocation.Longitude.Value, rfq.Latitude.Value, rfq.Longitude.Value) <= supplierLocation.MarketRadiusKm).ToArray();
        string cleanSearch = search?.Trim() ?? string.Empty;
        if (cleanSearch.Length > 0)
            rfqs = rfqs.Where(rfq =>
                rfq.Title.Contains(cleanSearch, StringComparison.OrdinalIgnoreCase)
                || (rfq.ProjectName?.Contains(cleanSearch, StringComparison.OrdinalIgnoreCase) ?? false)
                || rfq.DeliveryLocality.Contains(cleanSearch, StringComparison.OrdinalIgnoreCase)
                || rfq.Items.Any(item => item.ProductName.Contains(cleanSearch, StringComparison.OrdinalIgnoreCase)))
                .ToArray();

        Dictionary<Guid, HashSet<Guid>> matchingItems = [];
        rfqs = ApplyCoverage(rfqs, compatibleSupplierCategories, explicitlyMatchedRequestIds, matchingItems);

        Guid[] categoryIds = rfqs.SelectMany(rfq => rfq.RequiredCategoryIds).Distinct().ToArray();
        Guid[] buyerIds = rfqs.Select(rfq => rfq.BuyerId).Distinct().ToArray();
        Dictionary<Guid, string> categories = await dbContext.Categories.AsNoTracking()
            .Where(category => categoryIds.Contains(category.Id))
            .ToDictionaryAsync(category => category.Id, category => category.Name, cancellationToken);
        Dictionary<Guid, string> buyers = await dbContext.BuyerProfiles.AsNoTracking()
            .Where(profile => buyerIds.Contains(profile.Id))
            .ToDictionaryAsync(
                profile => profile.Id,
                profile => profile.CompanyName ?? profile.DisplayName,
                cancellationToken);
        return rfqs.Select(rfq => new OpenRfq(
            rfq,
            string.Join(", ", rfq.RequiredCategoryIds.Select(id => categories.GetValueOrDefault(id, "Categorie"))),
            buyers.GetValueOrDefault(rfq.BuyerId, "Cumpărător verificat"),
            matchingItems.TryGetValue(rfq.Id, out HashSet<Guid>? ids) && ids.Count < rfq.Items.Count,
            matchingItems.GetValueOrDefault(rfq.Id))).ToArray();
    }

    public async Task<OpenRfq?> GetOpenAsync(Guid supplierId, Guid actorId, Guid rfqId, CancellationToken cancellationToken)
    {
        OpenRfq? openRequest = (await GetOpenAsync(supplierId, actorId, null, null, cancellationToken))
            .SingleOrDefault(item => item.Rfq.Id == rfqId);
        if (openRequest is not null) return openRequest;

        // A request stops being "open" when the Buyer selects an offer. The supplier who
        // participated must still be able to reopen it to see their final offer outcome.
        if (!await dbContext.Offers.AsNoTracking()
                .AnyAsync(offer => offer.RfqId == rfqId && offer.SupplierId == supplierId, cancellationToken))
            return null;

        Rfq? rfq = await dbContext.Rfqs.AsNoTracking().Include(item => item.NeedCategories)
            .Include(item => item.Items)
            .SingleOrDefaultAsync(item => item.Id == rfqId, cancellationToken);
        if (rfq is null) return null;
        if (rfq.BuyerId == actorId) return null;

        string[] categoryNames = await dbContext.Categories.AsNoTracking()
            .Where(category => rfq.RequiredCategoryIds.Contains(category.Id))
            .Select(category => category.Name)
            .ToArrayAsync(cancellationToken);
        string buyerName = await dbContext.BuyerProfiles.AsNoTracking()
            .Where(profile => profile.Id == rfq.BuyerId)
            .Select(profile => profile.CompanyName ?? profile.DisplayName)
            .SingleOrDefaultAsync(cancellationToken) ?? "Cumpărător verificat";
        return new OpenRfq(rfq, string.Join(", ", categoryNames), buyerName);
    }

    private Task<Rfq?> Find(Guid buyerId, Guid rfqId, CancellationToken cancellationToken) =>
        dbContext.Rfqs.Include(rfq => rfq.NeedCategories).Include(rfq => rfq.Items)
            .SingleOrDefaultAsync(rfq => rfq.Id == rfqId && rfq.BuyerId == buyerId, cancellationToken);

    private async Task RejectOpenOffers(Guid rfqId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        Offer[] offers = await dbContext.Offers.Where(offer => offer.RfqId == rfqId).ToArrayAsync(cancellationToken);
        foreach (Offer offer in offers) offer.Reject(now);
    }

    private async Task SaveRfqChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new RfqConcurrencyException(
                "Cererea a fost modificată între timp. Reîncarcă pagina înainte de a continua.",
                exception);
        }
    }

    private async Task ValidateReferences(Guid buyerId, RfqDraft draft, CancellationToken cancellationToken)
    {
        Guid[] categoryIds = [draft.PrimaryCategoryId];
        int validCategories = await dbContext.Categories.CountAsync(
            category => categoryIds.Contains(category.Id) && category.IsActive, cancellationToken);
        if (validCategories != categoryIds.Length)
            throw new RfqRuleException("Una dintre categoriile selectate nu este disponibilă.");
        if (draft.ProjectTemplateId.HasValue)
        {
            bool validTemplate = await dbContext.ProjectTemplates.AnyAsync(template =>
                template.Id == draft.ProjectTemplateId.Value && template.IsActive, cancellationToken);
            if (!validTemplate)
                throw new RfqRuleException("Șablonul de proiect selectat nu este disponibil.");
        }
    }

    private async Task<RfqDraft> ResolveLocationAsync(Guid buyerId, RfqDraft draft, CancellationToken cancellationToken)
    {
        if (draft.LocationContext == RfqLocationContext.Personal)
        {
            var profile = await dbContext.BuyerProfiles.AsNoTracking().SingleOrDefaultAsync(item => item.Id == buyerId, cancellationToken)
                ?? throw new RfqRuleException("Completează mai întâi locația personală din Cont.");
            var location = profile.Location.Latitude.HasValue && profile.Location.Longitude.HasValue ? profile.Location
                : await locationGeocoder.ResolveAsync(new(profile.DefaultAddress, profile.DefaultCity, profile.DefaultCounty, profile.DefaultCountry), cancellationToken);
            return draft with { DeliveryAddress = location.Address ?? string.Empty, DeliveryLocality = location.Locality, County = location.County, Latitude = location.Latitude, Longitude = location.Longitude, SupplierSearchRadiusKm = draft.SupplierSearchRadiusKm > 0 ? draft.SupplierSearchRadiusKm : profile.MarketRadiusKm, BuyingCompanyId = null };
        }
        if (draft.LocationContext == RfqLocationContext.Company)
        {
            if (!draft.BuyingCompanyId.HasValue || !await companyAuthorizationService.CanRepresentCompanyAsync(buyerId, draft.BuyingCompanyId.Value, cancellationToken))
                throw new RfqRuleException("Selectează o firmă pe care o reprezinți.");
            var company = await dbContext.SupplierProfiles.AsNoTracking().SingleOrDefaultAsync(item => item.Id == draft.BuyingCompanyId.Value, cancellationToken)
                ?? throw new RfqRuleException("Firma selectată nu este disponibilă.");
            var location = company.Location.Latitude.HasValue && company.Location.Longitude.HasValue ? company.Location
                : await locationGeocoder.ResolveAsync(new(company.Address, company.City, company.County, company.Country), cancellationToken);
            return draft with { DeliveryAddress = location.Address ?? string.Empty, DeliveryLocality = location.Locality, County = location.County, Latitude = location.Latitude, Longitude = location.Longitude, SupplierSearchRadiusKm = draft.SupplierSearchRadiusKm > 0 ? draft.SupplierSearchRadiusKm : company.MarketRadiusKm };
        }
        var other = await locationGeocoder.ResolveAsync(new(draft.DeliveryAddress, draft.DeliveryLocality, draft.County, "România"), cancellationToken);
        return draft with { Latitude = other.Latitude, Longitude = other.Longitude, SupplierSearchRadiusKm = draft.SupplierSearchRadiusKm > 0 ? draft.SupplierSearchRadiusKm : 30, BuyingCompanyId = null };
    }

    private async Task NotifyMatchingSuppliersAsync(Rfq rfq, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var suppliers = await dbContext.SupplierProfiles.AsNoTracking()
            .Where(profile => profile.IsActive && profile.Id != rfq.BuyerId)
            .Select(profile => new { profile.Id, profile.County })
            .ToArrayAsync(cancellationToken);
        Guid[] supplierIds = suppliers.Select(supplier => supplier.Id).ToArray();
        var categoryRows = await dbContext.SupplierCategories.AsNoTracking()
            .Where(category => supplierIds.Contains(category.SupplierId))
            .Select(category => new { category.SupplierId, category.CategoryId })
            .ToArrayAsync(cancellationToken);
        Dictionary<Guid, Guid?> categoryParents = await dbContext.Categories.AsNoTracking()
            .Where(category => category.IsActive)
            .ToDictionaryAsync(category => category.Id, category => category.ParentId, cancellationToken);
        HashSet<Guid> nationwideSupplierIds = (await dbContext.SupplierDeliverySettings.AsNoTracking()
            .Where(settings => supplierIds.Contains(settings.SupplierId) && settings.NationwideDelivery)
            .Select(settings => settings.SupplierId)
            .ToArrayAsync(cancellationToken)).ToHashSet();
        Dictionary<Guid, string[]> deliveryAreas = (await dbContext.SupplierDeliveryAreas.AsNoTracking()
            .Where(area => supplierIds.Contains(area.SupplierId))
            .Select(area => new { area.SupplierId, area.County })
            .ToArrayAsync(cancellationToken))
            .GroupBy(area => area.SupplierId)
            .ToDictionary(group => group.Key, group => group.Select(area => area.County).ToArray());
        Dictionary<Guid, Guid[]> categoriesBySupplier = categoryRows.GroupBy(category => category.SupplierId)
            .ToDictionary(group => group.Key, group => group.Select(category => category.CategoryId).ToArray());
        HashSet<Guid> requiredCategories = rfq.RequiredCategoryIds.ToHashSet();
        HashSet<Guid> alreadyMatchedSupplierIds = (await dbContext.OpportunityMatches.AsNoTracking()
            .Where(match => match.RfqId == rfq.Id && match.SupplierProfileId != null)
            .Select(match => match.SupplierProfileId!.Value)
            .ToArrayAsync(cancellationToken)).ToHashSet();
        Dictionary<Guid, string> categoryNames = await dbContext.Categories.AsNoTracking()
            .Where(category => requiredCategories.Contains(category.Id))
            .ToDictionaryAsync(category => category.Id, category => category.Name, cancellationToken);

        foreach (var supplier in suppliers)
        {
            if (!nationwideSupplierIds.Contains(supplier.Id)
                && !(deliveryAreas.GetValueOrDefault(supplier.Id) ?? [supplier.County]).Contains(rfq.County))
                continue;
            if (!categoriesBySupplier.TryGetValue(supplier.Id, out Guid[]? selectedCategories)) continue;

            HashSet<Guid> compatibleCategories = CategoryCompatibilityPolicy.Expand(selectedCategories, categoryParents);
            if (ProviderCoveragePolicy.Decide(rfq.AllowSplit, requiredCategories, compatibleCategories) == ProviderCoverageMatch.None)
                continue;

            if (!alreadyMatchedSupplierIds.Contains(supplier.Id))
            {
                Guid matchingCategory = requiredCategories.First(compatibleCategories.Contains);
                dbContext.OpportunityMatches.Add(OpportunityMatch.ForCompany(
                    rfq.Id,
                    supplier.Id,
                    $"Categorie compatibilă: {categoryNames.GetValueOrDefault(matchingCategory, "categorie selectată")} · județ: {rfq.County}",
                    now));
            }

            foreach (Guid recipientId in await companyAuthorizationService.GetActiveMemberUserIdsAsync(supplier.Id, cancellationToken))
                dbContext.Notifications.Add(Notification.Create(
                    recipientId,
                    NotificationType.RequestPublished,
                    "Cerere nouă relevantă",
                    $"O cerere nouă, „{rfq.Title}”, se potrivește categoriilor și zonei tale.",
                    $"/opportunities/{rfq.Id}?companyId={supplier.Id}",
                    $"rfq:{rfq.Id}:published:{supplier.Id}:recipient:{recipientId}",
                    now));
        }

        var candidates = await dbContext.SupplierCandidates.AsNoTracking().Include(candidate => candidate.Categories)
            .Where(candidate => candidate.Status != SupplierCandidateStatus.Claimed
                && candidate.Status != SupplierCandidateStatus.Rejected
                && candidate.County == rfq.County)
            .ToArrayAsync(cancellationToken);
        HashSet<Guid> alreadyMatchedCandidateIds = (await dbContext.OpportunityMatches.AsNoTracking()
            .Where(match => match.RfqId == rfq.Id && match.SupplierCandidateId != null)
            .Select(match => match.SupplierCandidateId!.Value)
            .ToArrayAsync(cancellationToken)).ToHashSet();
        foreach (SupplierCandidate candidate in candidates)
        {
            if (alreadyMatchedCandidateIds.Contains(candidate.Id)) continue;
            HashSet<Guid> compatibleCategories = CategoryCompatibilityPolicy.Expand(candidate.CategoryIds, categoryParents);
            Guid? matchingCategory = requiredCategories.FirstOrDefault(compatibleCategories.Contains);
            if (!matchingCategory.HasValue || matchingCategory == Guid.Empty) continue;
            dbContext.OpportunityMatches.Add(OpportunityMatch.ForCandidate(
                rfq.Id, candidate.Id, OpportunityMatchSource.ExistingCandidateMatching,
                $"Categorie compatibilă: {categoryNames.GetValueOrDefault(matchingCategory.Value, "categorie selectată")} · județ: {rfq.County}", now));
        }
    }

    private static Rfq[] ApplyCoverage(
        Rfq[] rfqs,
        HashSet<Guid> providerCategories,
        HashSet<Guid> manuallyMatchedRequestIds,
        Dictionary<Guid, HashSet<Guid>> matchingItems)
    {
        List<Rfq> visible = [];
        foreach (Rfq rfq in rfqs)
        {
            HashSet<Guid> requiredCategories = rfq.RequiredCategoryIds.ToHashSet();
            ProviderCoverageMatch coverage = manuallyMatchedRequestIds.Contains(rfq.Id)
                ? ProviderCoverageMatch.Full
                : ProviderCoveragePolicy.Decide(rfq.AllowSplit, requiredCategories, providerCategories);
            if (coverage == ProviderCoverageMatch.None) continue;

            HashSet<Guid> itemIds = rfq.Items.Select(item => item.Id).ToHashSet();
            matchingItems[rfq.Id] = itemIds;
            visible.Add(rfq);
        }
        return visible.ToArray();
    }

}
