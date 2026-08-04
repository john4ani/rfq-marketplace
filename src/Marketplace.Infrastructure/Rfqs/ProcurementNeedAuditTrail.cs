using Marketplace.Application.Rfqs;
using Marketplace.Domain.Rfqs;
using Marketplace.Infrastructure.Persistence;

namespace Marketplace.Infrastructure.Rfqs;

public sealed class ProcurementNeedAuditTrail(MarketplaceDbContext dbContext) : IProcurementNeedAuditTrail
{
    public void Record(
        Guid procurementNeedId,
        Guid actorId,
        ProcurementNeedAuditEventType type,
        ProcurementNeedStatus fromStatus,
        ProcurementNeedStatus toStatus,
        string? details,
        DateTimeOffset occurredAt) =>
        dbContext.ProcurementNeedAuditEvents.Add(ProcurementNeedAuditEvent.Create(
            Guid.NewGuid(), procurementNeedId, actorId, type, fromStatus, toStatus, details, occurredAt));
}
