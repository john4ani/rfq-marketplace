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
}
