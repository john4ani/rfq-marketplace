using Marketplace.Domain.Rfqs;

namespace Marketplace.Application.Rfqs;

public interface IRfqService
{
    Task<IReadOnlyCollection<Rfq>> GetMineAsync(Guid buyerId, CancellationToken cancellationToken);
    Task<Rfq?> GetAsync(Guid buyerId, Guid rfqId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<ProcurementNeedAuditEvent>?> GetHistoryAsync(Guid buyerId, Guid rfqId, CancellationToken cancellationToken);
    Task<Rfq> CreateAsync(Guid buyerId, RfqDraft draft, CancellationToken cancellationToken);
    Task<Rfq?> UpdateAsync(Guid buyerId, Guid rfqId, RfqDraft draft, CancellationToken cancellationToken);
    Task<Rfq?> PublishAsync(Guid buyerId, Guid rfqId, CancellationToken cancellationToken);
    Task<Rfq?> CancelAsync(Guid buyerId, Guid rfqId, CancellationToken cancellationToken);
    Task<Rfq?> RecordOutcomeAsync(Guid buyerId, Guid rfqId, ProcurementOutcome outcome, string? reason, CancellationToken cancellationToken);
    Task<Rfq?> ExtendQuotationDeadlineAsync(Guid buyerId, Guid rfqId, DateTimeOffset newDeadline, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<OpenRfq>> GetOpenAsync(Guid supplierId, Guid actorId, string? search, Guid? categoryId, CancellationToken cancellationToken);
    Task<OpenRfq?> GetOpenAsync(Guid supplierId, Guid actorId, Guid rfqId, CancellationToken cancellationToken);
}

public interface IProcurementNeedAuditTrail
{
    void Record(
        Guid procurementNeedId,
        Guid actorId,
        ProcurementNeedAuditEventType type,
        ProcurementNeedStatus fromStatus,
        ProcurementNeedStatus toStatus,
        string? details,
        DateTimeOffset occurredAt);
}

public sealed record OpenRfq(
    Rfq Rfq,
    string CategoryName,
    string BuyerDisplayName,
    bool IsPartialMatch = false,
    IReadOnlySet<Guid>? MatchingItemIds = null);
