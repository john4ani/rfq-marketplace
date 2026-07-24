using Marketplace.Domain.Rfqs;

namespace Marketplace.Application.Rfqs;

public interface IRfqService
{
    Task<IReadOnlyCollection<Rfq>> GetMineAsync(Guid buyerId, CancellationToken cancellationToken);
    Task<Rfq?> GetAsync(Guid buyerId, Guid rfqId, CancellationToken cancellationToken);
    Task<Rfq> CreateAsync(Guid buyerId, RfqDraft draft, CancellationToken cancellationToken);
    Task<Rfq?> UpdateAsync(Guid buyerId, Guid rfqId, RfqDraft draft, CancellationToken cancellationToken);
    Task<Rfq?> PublishAsync(Guid buyerId, Guid rfqId, CancellationToken cancellationToken);
    Task<Rfq?> CancelAsync(Guid buyerId, Guid rfqId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<OpenRfq>> GetOpenAsync(Guid supplierId, string? search, Guid? categoryId, CancellationToken cancellationToken);
    Task<OpenRfq?> GetOpenAsync(Guid supplierId, Guid rfqId, CancellationToken cancellationToken);
}

public sealed record OpenRfq(Rfq Rfq, string CategoryName, string BuyerDisplayName);
