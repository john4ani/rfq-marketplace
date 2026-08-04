using Marketplace.Application.Offers;
using Marketplace.Application.Rfqs;
using Marketplace.Domain.Offers;
using Marketplace.Domain.Rfqs;
using Marketplace.Domain.Notifications;
using Marketplace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Infrastructure.Offers;

public sealed class OfferService(
    MarketplaceDbContext dbContext,
    TimeProvider timeProvider,
    IProcurementNeedAuditTrail auditTrail) : IOfferService
{
    public Task<Offer?> GetMineForRfqAsync(Guid supplierId, Guid rfqId, CancellationToken cancellationToken) =>
        dbContext.Offers.AsNoTracking().Include(offer => offer.Items)
            .SingleOrDefaultAsync(offer => offer.SupplierId == supplierId && offer.RfqId == rfqId, cancellationToken);

    public async Task<IReadOnlyCollection<OfferSummary>> GetMineAsync(Guid supplierId, CancellationToken cancellationToken)
    {
        var rows = await dbContext.Offers.AsNoTracking().Include(offer => offer.Items)
            .Where(offer => offer.SupplierId == supplierId)
            .Join(dbContext.Rfqs, offer => offer.RfqId, rfq => rfq.Id,
                (offer, rfq) => new { offer, rfq.Title, rfq.QuotationDeadline, rfq.FulfillmentStatus })
            .OrderByDescending(row => row.offer.UpdatedAt).ToArrayAsync(cancellationToken);
        return rows.Select(row => new OfferSummary(
            row.offer, row.Title, row.QuotationDeadline, row.FulfillmentStatus)).ToArray();
    }

    public async Task<Offer> SaveDraftAsync(Guid supplierId, Guid rfqId, OfferDraft draft, CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        EligibleOfferRequest eligible = await EligibleRfq(supplierId, rfqId, now, cancellationToken);
        Rfq rfq = eligible.Rfq;
        if (draft.PricingMode == OfferPricingMode.Total && eligible.Coverage != ProviderCoverageMatch.Full)
            throw new OfferRuleException("Oferta cu preț total este disponibilă doar furnizorilor care pot acoperi întreaga cerere.");
        Guid[] validItemIds = eligible.ItemIds.ToArray();
        if (draft.Items.Any(item => !validItemIds.Contains(item.RfqItemId)))
            throw new OfferRuleException("Oferta conține o poziție care nu aparține acestei cereri.");
        Offer? offer = await dbContext.Offers.Include(item => item.Items)
            .SingleOrDefaultAsync(item => item.SupplierId == supplierId && item.RfqId == rfqId, cancellationToken);
        if (offer is null) { offer = Offer.Create(Guid.NewGuid(), rfqId, supplierId, draft, now); dbContext.Offers.Add(offer); }
        else offer.Update(draft, now);
        await dbContext.SaveChangesAsync(cancellationToken);
        return offer;
    }

    public async Task<Offer?> SubmitAsync(Guid supplierId, Guid rfqId, CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        Rfq rfq = (await EligibleRfq(supplierId, rfqId, now, cancellationToken)).Rfq;
        Offer? offer = await dbContext.Offers.Include(item => item.Items)
            .SingleOrDefaultAsync(item => item.SupplierId == supplierId && item.RfqId == rfqId, cancellationToken);
        if (offer is null) return null;
        ProcurementNeedStatus previousStatus = rfq.NeedStatus;
        offer.Submit(now); rfq.MarkOfferReceived(now);
        auditTrail.Record(rfq.Id, supplierId, ProcurementNeedAuditEventType.OfferReceived,
            previousStatus, rfq.NeedStatus, null, now);
        string supplierName = await dbContext.SupplierProfiles.Where(profile => profile.Id == supplierId)
            .Select(profile => profile.TradingName).SingleAsync(cancellationToken);
        NotificationType notificationType = offer.RevisionNumber == 1 ? NotificationType.OfferReceived : NotificationType.OfferRevised;
        string action = notificationType == NotificationType.OfferReceived ? "a trimis o ofertă" : "a revizuit oferta";
        dbContext.Notifications.Add(Notification.Create(
            rfq.BuyerId, notificationType,
            notificationType == NotificationType.OfferReceived ? "Ofertă nouă primită" : "Ofertă revizuită",
            $"{supplierName} {action} pentru „{rfq.Title}”.",
            $"/rfqs/{rfq.Id}/offers",
            $"offer:{offer.Id}:revision:{offer.RevisionNumber}", now));
        await SaveRfqChangesAsync(cancellationToken);
        return offer;
    }

    public async Task<Offer?> WithdrawAsync(Guid supplierId, Guid offerId, CancellationToken cancellationToken)
    {
        Offer? offer = await dbContext.Offers.Include(item => item.Items)
            .SingleOrDefaultAsync(item => item.Id == offerId && item.SupplierId == supplierId, cancellationToken);
        if (offer is null) return null;
        offer.Withdraw(timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
        return offer;
    }

    public async Task<IReadOnlyCollection<BuyerOffer>> GetForBuyerAsync(Guid buyerId, Guid rfqId, CancellationToken cancellationToken)
    {
        if (!await dbContext.Rfqs.AnyAsync(rfq => rfq.Id == rfqId && rfq.BuyerId == buyerId, cancellationToken))
            return [];
        Offer[] offers = await dbContext.Offers.AsNoTracking().Include(offer => offer.Items)
            .Where(offer => offer.RfqId == rfqId && offer.Status != OfferStatus.Draft && offer.Status != OfferStatus.Withdrawn)
            .OrderBy(offer => offer.DeliveryCost).ToArrayAsync(cancellationToken);
        Guid[] supplierIds = offers.Select(offer => offer.SupplierId).ToArray();
        Dictionary<Guid, string> names = await dbContext.SupplierProfiles.AsNoTracking()
            .Where(profile => supplierIds.Contains(profile.Id)).ToDictionaryAsync(profile => profile.Id, profile => profile.TradingName, cancellationToken);
        return offers.Select(offer => new BuyerOffer(offer, names.GetValueOrDefault(offer.SupplierId, "Furnizor"))).OrderBy(item => item.Offer.Total).ToArray();
    }

    public async Task<Offer?> SelectAsync(Guid buyerId, Guid rfqId, Guid offerId, CancellationToken cancellationToken)
    {
        Rfq? rfq = await dbContext.Rfqs.SingleOrDefaultAsync(item => item.Id == rfqId && item.BuyerId == buyerId, cancellationToken);
        if (rfq is null) return null;
        Offer? selected = await dbContext.Offers.Include(offer => offer.Items)
            .SingleOrDefaultAsync(offer => offer.Id == offerId && offer.RfqId == rfqId, cancellationToken);
        if (selected is null || selected.Status is not (OfferStatus.Submitted or OfferStatus.Revised)) return null;
        DateTimeOffset now = timeProvider.GetUtcNow();
        ProcurementNeedStatus previousStatus = rfq.NeedStatus;
        foreach (Offer offer in await dbContext.Offers.Where(offer => offer.RfqId == rfqId).ToListAsync(cancellationToken))
            if (offer.Id == offerId) offer.Select(now); else offer.Reject(now);
        rfq.SelectOffer(offerId, selected.DeliveryIncluded, now);
        auditTrail.Record(rfq.Id, buyerId, ProcurementNeedAuditEventType.OfferSelected,
            previousStatus, rfq.NeedStatus, null, now);
        await SaveRfqChangesAsync(cancellationToken);
        return selected;
    }

    public async Task<Rfq?> ReportDeliveredAsync(
        Guid supplierId,
        Guid rfqId,
        CancellationToken cancellationToken)
    {
        Rfq? rfq = await dbContext.Rfqs.SingleOrDefaultAsync(item => item.Id == rfqId, cancellationToken);
        if (rfq is null) return null;
        Offer? selected = rfq.SelectedOfferId.HasValue
            ? await dbContext.Offers.SingleOrDefaultAsync(
                offer => offer.Id == rfq.SelectedOfferId && offer.SupplierId == supplierId,
                cancellationToken)
            : null;
        if (selected is null) return null;

        DateTimeOffset now = timeProvider.GetUtcNow();
        ProcurementNeedStatus previousStatus = rfq.NeedStatus;
        rfq.ReportDelivered(now);
        auditTrail.Record(rfq.Id, supplierId, ProcurementNeedAuditEventType.DeliveryReported,
            previousStatus, rfq.NeedStatus, null, now);
        string supplierName = await dbContext.SupplierProfiles
            .Where(profile => profile.Id == supplierId)
            .Select(profile => profile.TradingName)
            .SingleAsync(cancellationToken);
        dbContext.Notifications.Add(Notification.Create(
            rfq.BuyerId,
            NotificationType.DeliveryReported,
            "Livrare declarată",
            $"{supplierName} a declarat livrată cererea „{rfq.Title}”. Confirmă primirea după verificare.",
            $"/rfqs/{rfq.Id}/offers",
            $"rfq:{rfq.Id}:delivery-reported",
            now));
        await SaveRfqChangesAsync(cancellationToken);
        return rfq;
    }

    public async Task<Rfq?> ConfirmDeliveryAsync(
        Guid buyerId,
        Guid rfqId,
        CancellationToken cancellationToken)
    {
        Rfq? rfq = await dbContext.Rfqs.SingleOrDefaultAsync(
            item => item.Id == rfqId && item.BuyerId == buyerId,
            cancellationToken);
        if (rfq is null) return null;
        Offer? selected = rfq.SelectedOfferId.HasValue
            ? await dbContext.Offers.SingleOrDefaultAsync(
                offer => offer.Id == rfq.SelectedOfferId,
                cancellationToken)
            : null;
        if (selected is null) return null;

        DateTimeOffset now = timeProvider.GetUtcNow();
        ProcurementNeedStatus previousStatus = rfq.NeedStatus;
        rfq.ConfirmDelivery(now);
        auditTrail.Record(rfq.Id, buyerId, ProcurementNeedAuditEventType.Completed,
            previousStatus, rfq.NeedStatus, null, now);
        dbContext.Notifications.Add(Notification.Create(
            selected.SupplierId,
            NotificationType.DeliveryConfirmed,
            "Livrare confirmată",
            $"Cumpărătorul a confirmat primirea pentru cererea „{rfq.Title}”.",
            $"/open-requests/{rfq.Id}",
            $"rfq:{rfq.Id}:delivery-confirmed",
            now));
        await SaveRfqChangesAsync(cancellationToken);
        return rfq;
    }

    public async Task<Rfq?> ReopenAsync(
        Guid buyerId,
        Guid rfqId,
        string reason,
        DateTimeOffset? newQuotationDeadline,
        CancellationToken cancellationToken)
    {
        Rfq? rfq = await dbContext.Rfqs.SingleOrDefaultAsync(
            item => item.Id == rfqId && item.BuyerId == buyerId, cancellationToken);
        if (rfq is null) return null;

        DateTimeOffset now = timeProvider.GetUtcNow();
        ProcurementNeedStatus previousStatus = rfq.NeedStatus;
        rfq.Reopen(reason, newQuotationDeadline, now);
        auditTrail.Record(rfq.Id, buyerId, ProcurementNeedAuditEventType.Reopened,
            previousStatus, rfq.NeedStatus, reason, now);
        Offer[] offers = await dbContext.Offers
            .Where(offer => offer.RfqId == rfqId)
            .ToArrayAsync(cancellationToken);
        foreach (Offer offer in offers) offer.RestoreAfterRequestReopened(now);

        string cleanReason = reason.Trim();
        foreach (Guid supplierId in offers.Select(offer => offer.SupplierId).Distinct())
            dbContext.Notifications.Add(Notification.Create(
                supplierId,
                NotificationType.RequestReopened,
                "Cerere redeschisă",
                $"Cererea „{rfq.Title}” a fost redeschisă. Motiv: {cleanReason}",
                $"/open-requests/{rfq.Id}",
                $"rfq:{rfq.Id}:reopened:{now.ToUnixTimeMilliseconds()}:{supplierId}",
                now));

        await SaveRfqChangesAsync(cancellationToken);
        return rfq;
    }

    private async Task<EligibleOfferRequest> EligibleRfq(
        Guid supplierId,
        Guid rfqId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        Rfq? rfq = await dbContext.Rfqs.Include(item => item.Items).SingleOrDefaultAsync(item =>
            item.Id == rfqId && (item.Status == RfqStatus.Published || item.Status == RfqStatus.ReceivingOffers)
            && item.QuotationDeadline > now, cancellationToken);
        if (rfq is null) throw new OfferRuleException("Cererea nu mai primește oferte.");

        HashSet<Guid> requiredCategories = rfq.Items.Count > 0
            ? rfq.Items.Select(item => item.CategoryId ?? rfq.PrimaryCategoryId).ToHashSet()
            : [rfq.PrimaryCategoryId];
        var profiles = await dbContext.SupplierProfiles.AsNoTracking().Where(profile => profile.IsActive)
            .Select(profile => new { profile.Id, profile.County }).ToArrayAsync(cancellationToken);
        var currentProfile = profiles.SingleOrDefault(profile => profile.Id == supplierId)
            ?? throw new OfferRuleException("Profilul furnizorului nu este activ.");
        var categoryRows = await dbContext.SupplierCategories.AsNoTracking()
            .Select(item => new { item.SupplierId, item.CategoryId }).ToArrayAsync(cancellationToken);
        Dictionary<Guid, HashSet<Guid>> categoriesBySupplier = categoryRows.GroupBy(item => item.SupplierId)
            .ToDictionary(group => group.Key, group => group.Select(item => item.CategoryId).ToHashSet());
        if (!categoriesBySupplier.TryGetValue(supplierId, out HashSet<Guid>? currentCategories))
            throw new OfferRuleException("Selectați categoriile furnizate înainte de a oferta.");

        var settingsRows = await dbContext.SupplierDeliverySettings.AsNoTracking()
            .Select(settings => new {
                settings.SupplierId,
                settings.NationwideDelivery,
                settings.CanFulfillMultiCategoryRequests
            }).ToArrayAsync(cancellationToken);
        HashSet<Guid> nationwideSuppliers = settingsRows.Where(settings => settings.NationwideDelivery)
            .Select(settings => settings.SupplierId).ToHashSet();
        HashSet<Guid> multiCategorySuppliers = settingsRows.Where(settings => settings.CanFulfillMultiCategoryRequests)
            .Select(settings => settings.SupplierId).ToHashSet();
        var areaRows = await dbContext.SupplierDeliveryAreas.AsNoTracking()
            .Select(area => new { area.SupplierId, area.County }).ToArrayAsync(cancellationToken);
        Dictionary<Guid, HashSet<string>> areasBySupplier = areaRows.GroupBy(area => area.SupplierId)
            .ToDictionary(group => group.Key, group => group.Select(area => area.County).ToHashSet(StringComparer.OrdinalIgnoreCase));

        bool DeliversToRequest(Guid id, string profileCounty) =>
            nationwideSuppliers.Contains(id)
            || (areasBySupplier.TryGetValue(id, out HashSet<string>? areas)
                ? areas.Contains(rfq.County)
                : string.Equals(profileCounty, rfq.County, StringComparison.OrdinalIgnoreCase));

        if (!DeliversToRequest(supplierId, currentProfile.County))
            throw new OfferRuleException("Cererea nu este disponibilă în zona de livrare configurată.");

        bool fullCoverageExists = profiles.Where(profile => DeliversToRequest(profile.Id, profile.County)).Any(profile =>
            categoriesBySupplier.TryGetValue(profile.Id, out HashSet<Guid>? categories)
            && requiredCategories.IsSubsetOf(categories)
            && (requiredCategories.Count == 1 || multiCategorySuppliers.Contains(profile.Id)));
        ProviderCoverageMatch coverage = ProviderCoveragePolicy.Decide(
            rfq.AllowSplit,
            requiredCategories,
            currentCategories,
            multiCategorySuppliers.Contains(supplierId),
            fullCoverageExists);
        if (coverage == ProviderCoverageMatch.None)
            throw new OfferRuleException("Cererea nu este disponibilă pentru profilul și zona configurate.");

        HashSet<Guid> itemIds = coverage == ProviderCoverageMatch.Full
            ? rfq.Items.Select(item => item.Id).ToHashSet()
            : rfq.Items.Where(item => currentCategories.Contains(item.CategoryId ?? rfq.PrimaryCategoryId))
                .Select(item => item.Id).ToHashSet();
        return new(rfq, coverage, itemIds);
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

    private sealed record EligibleOfferRequest(
        Rfq Rfq,
        ProviderCoverageMatch Coverage,
        IReadOnlySet<Guid> ItemIds);
}
