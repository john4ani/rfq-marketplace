using Marketplace.Domain.Rfqs;

namespace Marketplace.Application.Operations;

public interface IConciergeService
{
    Task<ConciergeOverview> GetOverviewAsync(CancellationToken cancellationToken);
    Task<IReadOnlyCollection<ConciergeSupplier>> FindSuppliersAsync(string? search, CancellationToken cancellationToken);
    Task CreateMatchOverrideAsync(Guid administratorId, Guid rfqId, Guid supplierId, string reason, CancellationToken cancellationToken);
    Task RequestBuyerCorrectionAsync(Guid administratorId, Guid rfqId, string note, CancellationToken cancellationToken);
    Task RequestOfferNormalizationAsync(Guid administratorId, Guid offerId, string note, CancellationToken cancellationToken);
    Task RecordOutcomeAsync(Guid administratorId, Guid rfqId, ProcurementOutcome outcome, string? note, CancellationToken cancellationToken);
}

public sealed record ConciergeSupplier(Guid Id, string TradingName, string County, IReadOnlyCollection<string> Categories);
public sealed record ConciergeRequest(Guid Id, string Title, string BuyerName, string County, RfqStatus Status,
    DateTimeOffset QuotationDeadline, int OfferCount, bool HasManualMatch);
public sealed record ConciergeOffer(Guid Id, Guid RfqId, string RfqTitle, string SupplierName, bool Comparable,
    string Coverage, DateTimeOffset UpdatedAt);
public sealed record AiFailureReview(Guid EventId, Guid SessionId, string? Provider, string? Model, DateTimeOffset OccurredAt);
public sealed record ConciergeMetric(string Name, int Value);
public sealed record ConciergeOverview(IReadOnlyCollection<ConciergeMetric> Metrics,
    IReadOnlyCollection<ConciergeRequest> OpenRequests, IReadOnlyCollection<ConciergeOffer> OffersNeedingReview,
    IReadOnlyCollection<AiFailureReview> AiFailures);

public sealed class ConciergeRuleException(string message) : InvalidOperationException(message);
