using Marketplace.Application.Operations;
using Marketplace.Domain.Operations;
using Marketplace.Infrastructure.Persistence;

namespace Marketplace.Infrastructure.Operations;

internal sealed class ProductEventService(MarketplaceDbContext dbContext) : IProductEventService
{
    public void Record(string name, string subjectType, Guid subjectId, Guid? actorId, string? metadata, DateTimeOffset occurredAt) =>
        dbContext.ProductEvents.Add(ProductEvent.Create(name, subjectType, subjectId, actorId, metadata, occurredAt));
}
