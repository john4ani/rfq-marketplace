using Marketplace.Application.Categories;
using Marketplace.Application.Companies;
using Marketplace.Application.Rfqs;
using Marketplace.Application.Operations;
using Marketplace.Domain.Offers;
using Marketplace.Domain.Notifications;
using Marketplace.Domain.Rfqs;
using Marketplace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Infrastructure.Rfqs;

public sealed class RfqService(
    MarketplaceDbContext dbContext,
    ICompanyAuthorizationService companyAuthorizationService,
    TimeProvider timeProvider,
    IProcurementNeedAuditTrail auditTrail,
    IProductEventService productEvents) : IRfqService
{
    public async Task<IReadOnlyCollection<Rfq>> GetMineAsync(Guid buyerId, CancellationToken cancellationToken) =>
        await dbContext.Rfqs.AsNoTracking().Include(rfq => rfq.NeedCategories)
            .Include(rfq => rfq.Items).ThenInclude(item => item.NeedCategory).Where(rfq => rfq.BuyerId == buyerId)
            .OrderByDescending(rfq => rfq.UpdatedAt).ToListAsync(cancellationToken);

    public Task<Rfq?> GetAsync(Guid buyerId, Guid rfqId, CancellationToken cancellationToken) =>
        dbContext.Rfqs.AsNoTracking().Include(rfq => rfq.NeedCategories)
            .Include(rfq => rfq.Items).ThenInclude(item => item.NeedCategory)
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
        Guid[] supplierCategoryIds = await dbContext.SupplierCategories.AsNoTracking()
            .Where(selection => selection.SupplierId == supplierId)
            .Select(selection => selection.CategoryId)
            .ToArrayAsync(cancellationToken);
        if (supplierCategoryIds.Length == 0 && manuallyMatchedRequestIds.Length == 0) return [];
        Dictionary<Guid, Guid?> categoryParents = await dbContext.Categories.AsNoTracking()
            .Where(category => category.IsActive)
            .ToDictionaryAsync(category => category.Id, category => category.ParentId, cancellationToken);
        HashSet<Guid> compatibleSupplierCategories = CategoryCompatibilityPolicy.Expand(supplierCategoryIds, categoryParents);
        Guid[] compatibleSupplierCategoryIds = compatibleSupplierCategories.ToArray();

        string? supplierCounty = await dbContext.SupplierProfiles.AsNoTracking()
            .Where(profile => profile.Id == supplierId && profile.IsActive)
            .Select(profile => profile.County)
            .SingleOrDefaultAsync(cancellationToken);
        if (supplierCounty is null) return [];

        bool nationwide = await dbContext.SupplierDeliverySettings.AsNoTracking()
            .Where(settings => settings.SupplierId == supplierId)
            .Select(settings => settings.NationwideDelivery)
            .SingleOrDefaultAsync(cancellationToken);
        string[] configuredCounties = await dbContext.SupplierDeliveryAreas.AsNoTracking()
            .Where(area => area.SupplierId == supplierId)
            .Select(area => area.County)
            .ToArrayAsync(cancellationToken);
        string[] deliveryCounties = configuredCounties.Length > 0 ? configuredCounties : [supplierCounty];

        IQueryable<Rfq> query = dbContext.Rfqs.AsNoTracking().Include(rfq => rfq.NeedCategories)
            .Include(rfq => rfq.Items).ThenInclude(item => item.NeedCategory)
            .Where(rfq => (rfq.Status == RfqStatus.Published || rfq.Status == RfqStatus.ReceivingOffers)
                && rfq.QuotationDeadline > now
                && rfq.BuyerId != actorId
                && (manuallyMatchedRequestIds.Contains(rfq.Id)
                    || (rfq.NeedCategories.Any(category => compatibleSupplierCategoryIds.Contains(category.CategoryId))
                        && (nationwide || deliveryCounties.Contains(rfq.County)))));
        if (categoryId.HasValue)
        {
            Guid[] compatibleFilterCategoryIds = CategoryCompatibilityPolicy.Expand([categoryId.Value], categoryParents).ToArray();
            query = query.Where(rfq => rfq.NeedCategories.Any(category => compatibleFilterCategoryIds.Contains(category.CategoryId)));
        }

        Rfq[] rfqs = await query.OrderBy(rfq => rfq.QuotationDeadline).ThenByDescending(rfq => rfq.PublishedAt)
            .ToArrayAsync(cancellationToken);
        string cleanSearch = search?.Trim() ?? string.Empty;
        if (cleanSearch.Length > 0)
            rfqs = rfqs.Where(rfq =>
                rfq.Title.Contains(cleanSearch, StringComparison.OrdinalIgnoreCase)
                || (rfq.ProjectName?.Contains(cleanSearch, StringComparison.OrdinalIgnoreCase) ?? false)
                || rfq.DeliveryLocality.Contains(cleanSearch, StringComparison.OrdinalIgnoreCase)
                || rfq.Items.Any(item => item.ProductName.Contains(cleanSearch, StringComparison.OrdinalIgnoreCase)))
                .ToArray();

        Dictionary<Guid, HashSet<Guid>> matchingItems = [];
        rfqs = ApplyCoverage(rfqs, compatibleSupplierCategories, manuallyMatchedRequestIds.ToHashSet(), matchingItems);

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
            .Include(item => item.Items).ThenInclude(item => item.NeedCategory)
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
        dbContext.Rfqs.Include(rfq => rfq.NeedCategories).Include(rfq => rfq.Items).ThenInclude(item => item.NeedCategory)
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
        Guid[] categoryIds = (draft.CategoryIds ?? []).Concat(
                draft.Items.Where(item => item.CategoryId.HasValue).Select(item => item.CategoryId!.Value))
            .Append(draft.PrimaryCategoryId).Where(id => id != Guid.Empty).Distinct().ToArray();
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

    private async Task NotifyMatchingSuppliersAsync(Rfq rfq, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var suppliers = await dbContext.SupplierProfiles.AsNoTracking()
            .Where(profile => profile.IsActive && profile.Id != rfq.BuyerId)
            .Select(profile => new { profile.Id, profile.County })
            .ToArrayAsync(cancellationToken);
        if (suppliers.Length == 0) return;

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

        foreach (var supplier in suppliers)
        {
            if (!nationwideSupplierIds.Contains(supplier.Id)
                && !(deliveryAreas.GetValueOrDefault(supplier.Id) ?? [supplier.County]).Contains(rfq.County))
                continue;
            if (!categoriesBySupplier.TryGetValue(supplier.Id, out Guid[]? selectedCategories)) continue;

            HashSet<Guid> compatibleCategories = CategoryCompatibilityPolicy.Expand(selectedCategories, categoryParents);
            if (ProviderCoveragePolicy.Decide(rfq.AllowSplit, requiredCategories, compatibleCategories) == ProviderCoverageMatch.None)
                continue;

            foreach (Guid recipientId in await companyAuthorizationService.GetActiveMemberUserIdsAsync(supplier.Id, cancellationToken))
                dbContext.Notifications.Add(Notification.Create(
                    recipientId,
                    NotificationType.RequestPublished,
                    "Cerere nouă relevantă",
                    $"O cerere nouă, „{rfq.Title}”, se potrivește categoriilor și zonei tale.",
                    $"/open-requests/{rfq.Id}",
                    $"rfq:{rfq.Id}:published:{supplier.Id}:recipient:{recipientId}",
                    now));
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

            HashSet<Guid> itemIds = coverage == ProviderCoverageMatch.Full
                ? rfq.Items.Select(item => item.Id).ToHashSet()
                : rfq.Items.Where(item => providerCategories.Contains(item.CategoryId))
                    .Select(item => item.Id).ToHashSet();
            matchingItems[rfq.Id] = itemIds;
            visible.Add(rfq);
        }
        return visible.ToArray();
    }

}
