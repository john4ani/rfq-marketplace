using Marketplace.Application.Operations;
using Marketplace.Application.Rfqs;
using Marketplace.Domain.Notifications;
using Marketplace.Domain.Offers;
using Marketplace.Domain.Operations;
using Marketplace.Domain.Rfqs;
using Marketplace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Infrastructure.Operations;

internal sealed class ConciergeService(
    MarketplaceDbContext dbContext,
    IProcurementNeedAuditTrail auditTrail,
    IProductEventService productEvents,
    TimeProvider timeProvider) : IConciergeService
{
    public async Task<ConciergeOverview> GetOverviewAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        var openRows = await dbContext.Rfqs.AsNoTracking()
            .Where(item => item.Status == RfqStatus.Published || item.Status == RfqStatus.ReceivingOffers)
            .OrderBy(item => item.QuotationDeadline).Take(40)
            .Select(item => new { item.Id, item.Title, item.BuyerId, item.County, item.Status, item.QuotationDeadline })
            .ToArrayAsync(cancellationToken);
        Guid[] requestIds = openRows.Select(item => item.Id).ToArray();
        Guid[] buyerIds = openRows.Select(item => item.BuyerId).Distinct().ToArray();
        Dictionary<Guid, string> buyerNames = await dbContext.BuyerProfiles.AsNoTracking()
            .Where(item => buyerIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, item => item.DisplayName, cancellationToken);
        Dictionary<Guid, int> offerCounts = await dbContext.Offers.AsNoTracking().Where(item => requestIds.Contains(item.RfqId)
            && (item.Status == OfferStatus.Submitted || item.Status == OfferStatus.Revised)).GroupBy(item => item.RfqId)
            .ToDictionaryAsync(group => group.Key, group => group.Count(), cancellationToken);
        HashSet<Guid> overridden = (await dbContext.ConciergeMatchOverrides.AsNoTracking()
            .Where(item => requestIds.Contains(item.RfqId)).Select(item => item.RfqId).ToArrayAsync(cancellationToken)).ToHashSet();
        ConciergeRequest[] openRequests = openRows.Select(item => new ConciergeRequest(item.Id, item.Title,
            buyerNames.GetValueOrDefault(item.BuyerId, "Cumpărător"), item.County, item.Status, item.QuotationDeadline,
            offerCounts.GetValueOrDefault(item.Id), overridden.Contains(item.Id))).ToArray();

        var offerRows = await dbContext.Offers.AsNoTracking()
            .Where(item => item.Status == OfferStatus.Submitted || item.Status == OfferStatus.Revised)
            .Where(item => item.TaxTreatment == OfferTaxTreatment.NotSpecified
                || item.DeliveryTerms == OfferDeliveryTerms.ArrangedSeparately)
            .OrderByDescending(item => item.UpdatedAt).Take(30)
            .Join(dbContext.Rfqs.AsNoTracking(), offer => offer.RfqId, request => request.Id,
                (offer, request) => new { offer, request.Title })
            .Join(dbContext.SupplierProfiles.AsNoTracking(), row => row.offer.SupplierId, supplier => supplier.Id,
                (row, supplier) => new { row.offer.Id, row.offer.RfqId, row.Title, supplier.TradingName,
                    row.offer.TaxTreatment, row.offer.DeliveryTerms, row.offer.Coverage, row.offer.UpdatedAt })
            .ToArrayAsync(cancellationToken);
        ConciergeOffer[] offers = offerRows.Select(row => new ConciergeOffer(row.Id, row.RfqId, row.Title,
            row.TradingName, row.TaxTreatment != OfferTaxTreatment.NotSpecified
                && row.DeliveryTerms != OfferDeliveryTerms.ArrangedSeparately, row.Coverage.ToString(), row.UpdatedAt)).ToArray();
        AiFailureReview[] failures = await dbContext.ProductEvents.AsNoTracking().Where(item => item.Name == "AiIntakeFailed")
            .OrderByDescending(item => item.OccurredAt).Take(20)
            .Join(dbContext.AiIntakeSessions.AsNoTracking(), item => item.SubjectId, session => session.Id,
                (item, session) => new AiFailureReview(item.Id, session.Id,
                    string.IsNullOrWhiteSpace(session.Provider) ? null : session.Provider,
                    string.IsNullOrWhiteSpace(session.Model) ? null : session.Model, item.OccurredAt))
            .ToArrayAsync(cancellationToken);
        int published = await dbContext.Rfqs.CountAsync(item => item.Status == RfqStatus.Published || item.Status == RfqStatus.ReceivingOffers, cancellationToken);
        int outcomes = await dbContext.Rfqs.CountAsync(item => item.OutcomeRecordedAt != null, cancellationToken);
        return new([
            new("Cereri deschise", published), new("Cereri fără ofertă", openRequests.Count(item => item.OfferCount == 0)),
            new("Oferte de normalizat", offers.Length), new("Rezultate înregistrate", outcomes), new("Intervenții AI", failures.Length)
        ], openRequests, offers, failures);
    }

    public async Task<IReadOnlyCollection<ConciergeSupplier>> FindSuppliersAsync(string? search, CancellationToken cancellationToken)
    {
        string term = search?.Trim() ?? string.Empty;
        var rows = await dbContext.SupplierProfiles.AsNoTracking().Where(item => item.IsActive
                && (term.Length == 0 || EF.Functions.ILike(item.TradingName, $"%{term}%")))
            .OrderBy(item => item.TradingName).Take(30).Select(item => new { item.Id, item.TradingName, item.County }).ToArrayAsync(cancellationToken);
        Guid[] ids = rows.Select(item => item.Id).ToArray();
        var categories = await dbContext.SupplierCategories.AsNoTracking().Where(item => ids.Contains(item.SupplierId))
            .Join(dbContext.Categories.AsNoTracking(), item => item.CategoryId, category => category.Id,
                (item, category) => new { item.SupplierId, category.Name }).ToArrayAsync(cancellationToken);
        return rows.Select(item => new ConciergeSupplier(item.Id, item.TradingName, item.County,
            categories.Where(category => category.SupplierId == item.Id).Select(category => category.Name).ToArray())).ToArray();
    }

    public async Task CreateMatchOverrideAsync(Guid administratorId, Guid rfqId, Guid supplierId, string reason, CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        bool requestOpen = await dbContext.Rfqs.AnyAsync(item => item.Id == rfqId
            && (item.Status == RfqStatus.Published || item.Status == RfqStatus.ReceivingOffers) && item.QuotationDeadline > now, cancellationToken);
        bool supplierExists = await dbContext.SupplierProfiles.AnyAsync(item => item.Id == supplierId && item.IsActive, cancellationToken);
        if (!requestOpen || !supplierExists) throw new ConciergeRuleException("Cererea trebuie să fie deschisă, iar furnizorul activ.");
        if (await dbContext.ConciergeMatchOverrides.AnyAsync(item => item.RfqId == rfqId && item.SupplierId == supplierId, cancellationToken))
            throw new ConciergeRuleException("Furnizorul are deja o potrivire manuală pentru această cerere.");
        dbContext.ConciergeMatchOverrides.Add(ConciergeMatchOverride.Create(rfqId, supplierId, administratorId, reason, now));
        dbContext.Notifications.Add(Notification.Create(supplierId, NotificationType.ConciergeManualMatch,
            "Oportunitate potrivită manual", "Un administrator a verificat că poți răspunde la această cerere.",
            $"/open-requests/{rfqId}", $"concierge-match:{rfqId}:{supplierId}", now));
        productEvents.Record("ConciergeMatchOverrideCreated", "ProcurementNeed", rfqId, administratorId, $"SupplierId={supplierId}", now);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RequestBuyerCorrectionAsync(Guid administratorId, Guid rfqId, string note, CancellationToken cancellationToken)
    {
        Rfq? request = await dbContext.Rfqs.SingleOrDefaultAsync(item => item.Id == rfqId, cancellationToken);
        if (request is null) throw new ConciergeRuleException("Cererea nu există.");
        DateTimeOffset now = timeProvider.GetUtcNow();
        dbContext.Notifications.Add(Notification.Create(request.BuyerId, NotificationType.ConciergeCorrectionRequested,
            "Este nevoie de o clarificare", $"Un administrator te roagă să verifici cererea: {CleanNote(note)}",
            $"/rfqs/{rfqId}/edit", $"concierge-correction:{rfqId}:{now.ToUnixTimeMilliseconds()}", now));
        productEvents.Record("ConciergeCorrectionRequested", "ProcurementNeed", rfqId, administratorId, null, now);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RequestOfferNormalizationAsync(Guid administratorId, Guid offerId, string note, CancellationToken cancellationToken)
    {
        Offer? offer = await dbContext.Offers.SingleOrDefaultAsync(item => item.Id == offerId, cancellationToken);
        if (offer is null || offer.Status is not (OfferStatus.Submitted or OfferStatus.Revised)) throw new ConciergeRuleException("Oferta nu mai poate fi revizuită.");
        DateTimeOffset now = timeProvider.GetUtcNow();
        dbContext.Notifications.Add(Notification.Create(offer.SupplierId, NotificationType.ConciergeOfferNormalizationRequested,
            "Te rugăm să clarifici oferta", $"Un administrator a cerut clarificarea ofertei: {CleanNote(note)}",
            $"/open-requests/{offer.RfqId}", $"concierge-normalize:{offerId}:{now.ToUnixTimeMilliseconds()}", now));
        productEvents.Record("ConciergeOfferNormalizationRequested", "Offer", offerId, administratorId, null, now);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RecordOutcomeAsync(Guid administratorId, Guid rfqId, ProcurementOutcome outcome, string? note, CancellationToken cancellationToken)
    {
        Rfq? request = await dbContext.Rfqs.Include(item => item.NeedCategories).Include(item => item.Items)
            .SingleOrDefaultAsync(item => item.Id == rfqId, cancellationToken);
        if (request is null) throw new ConciergeRuleException("Cererea nu există.");
        DateTimeOffset now = timeProvider.GetUtcNow();
        ProcurementNeedStatus before = request.NeedStatus;
        request.RecordOutcome(outcome, note, now);
        foreach (Offer offer in await dbContext.Offers.Where(item => item.RfqId == rfqId).ToArrayAsync(cancellationToken)) offer.Reject(now);
        auditTrail.Record(rfqId, administratorId, ProcurementNeedAuditEventType.OutcomeRecorded, before, request.NeedStatus,
            $"Outcome={outcome}; RecordedBy=Administrator", now);
        productEvents.Record("ConciergeOutcomeRecorded", "ProcurementNeed", rfqId, administratorId, $"Outcome={outcome}", now);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string CleanNote(string note)
    {
        string clean = note?.Trim() ?? string.Empty;
        if (clean.Length is < 5 or > 1000) throw new ConciergeRuleException("Explicația trebuie să aibă între 5 și 1.000 de caractere.");
        return clean;
    }
}
