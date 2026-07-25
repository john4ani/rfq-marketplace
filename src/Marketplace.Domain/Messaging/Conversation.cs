namespace Marketplace.Domain.Messaging;

public sealed class Conversation
{
    private Conversation() { }
    public Guid Id { get; private set; }
    public Guid OfferId { get; private set; }
    public Guid BuyerId { get; private set; }
    public Guid SupplierId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? BuyerLastReadAt { get; private set; }
    public DateTimeOffset? SupplierLastReadAt { get; private set; }

    public static Conversation Create(Guid offerId, Guid buyerId, Guid supplierId, DateTimeOffset now) =>
        new() { Id = offerId, OfferId = offerId, BuyerId = buyerId, SupplierId = supplierId, CreatedAt = now, UpdatedAt = now };

    public void MessageSent(DateTimeOffset now) => UpdatedAt = now;

    public void MarkRead(Guid userId, DateTimeOffset now)
    {
        if (userId == BuyerId) BuyerLastReadAt = now;
        else if (userId == SupplierId) SupplierLastReadAt = now;
        else throw new MessagingRuleException("Nu ai acces la această conversație.");
    }
}

public sealed class MessagingRuleException(string message) : InvalidOperationException(message);
