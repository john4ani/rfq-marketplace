namespace Marketplace.Application.Messaging;

public interface IMessagingService
{
    Task<ConversationView?> GetAsync(Guid userId, Guid offerId, DateTimeOffset? after, CancellationToken cancellationToken);
    Task<ConversationMessageView?> SendAsync(Guid userId, Guid offerId, string body, CancellationToken cancellationToken);
    Task<bool> MarkReadAsync(Guid userId, Guid offerId, CancellationToken cancellationToken);
}

public sealed record ConversationView(
    Guid OfferId,
    Guid RfqId,
    string RfqTitle,
    string OtherPartyName,
    string ViewerRole,
    int UnreadCount,
    IReadOnlyCollection<ConversationMessageView> Messages);

public sealed record ConversationMessageView(Guid Id, string Body, DateTimeOffset CreatedAt, bool IsMine);
