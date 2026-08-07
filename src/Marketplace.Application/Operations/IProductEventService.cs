namespace Marketplace.Application.Operations;

public interface IProductEventService
{
    void Record(string name, string subjectType, Guid subjectId, Guid? actorId, string? metadata, DateTimeOffset occurredAt);
}
