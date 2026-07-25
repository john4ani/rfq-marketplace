using Marketplace.Domain.Offers;
using Marketplace.Domain.Rfqs;

namespace Marketplace.Application.Offers;

public interface IOfferService
{
    Task<Offer?> GetMineForRfqAsync(Guid supplierId, Guid rfqId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<OfferSummary>> GetMineAsync(Guid supplierId, CancellationToken cancellationToken);
    Task<Offer> SaveDraftAsync(Guid supplierId, Guid rfqId, OfferDraft draft, CancellationToken cancellationToken);
    Task<Offer?> SubmitAsync(Guid supplierId, Guid rfqId, CancellationToken cancellationToken);
    Task<Offer?> WithdrawAsync(Guid supplierId, Guid offerId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<BuyerOffer>> GetForBuyerAsync(Guid buyerId, Guid rfqId, CancellationToken cancellationToken);
    Task<Offer?> SelectAsync(Guid buyerId, Guid rfqId, Guid offerId, CancellationToken cancellationToken);
    Task<Rfq?> ReopenAsync(Guid buyerId, Guid rfqId, string reason, DateTimeOffset? newQuotationDeadline, CancellationToken cancellationToken);
    Task<Rfq?> ReportDeliveredAsync(Guid supplierId, Guid rfqId, CancellationToken cancellationToken);
    Task<Rfq?> ConfirmDeliveryAsync(Guid buyerId, Guid rfqId, CancellationToken cancellationToken);
}

public sealed record OfferSummary(
    Offer Offer,
    string RfqTitle,
    DateTimeOffset QuotationDeadline,
    FulfillmentStatus FulfillmentStatus);
public sealed record BuyerOffer(Offer Offer, string SupplierName);
