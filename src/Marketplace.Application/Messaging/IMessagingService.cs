namespace Marketplace.Application.Messaging;

public interface IMessagingService
{
    Task<ConversationView?> GetAsync(Guid userId, Guid offerId, DateTimeOffset? after, CancellationToken cancellationToken);
    Task<ConversationMessageView?> SendAsync(Guid userId, Guid offerId, string body, CancellationToken cancellationToken);
    Task<bool> MarkReadAsync(Guid userId, Guid offerId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<ConversationSummaryView>> ListConversationsAsync(Guid userId, CancellationToken cancellationToken);
}

// One row of the unified Mesaje inbox. The same list serves a User whether they are buying or representing a company;
// ViewerCompanyName is the represented company when the User is on the selling side (null when buying), so the UI can
// show "Cumpărare · <title>" vs "<Company> · <title>" without exposing membership internals.
public sealed record ConversationSummaryView(
    Guid OfferId,
    Guid RfqId,
    string RfqTitle,
    string OtherPartyName,
    string ViewerRole,
    string? ViewerCompanyName,
    string LastMessagePreview,
    DateTimeOffset LastActivityAt,
    int UnreadCount);

public sealed record ConversationView(
    Guid OfferId,
    Guid RfqId,
    string RfqTitle,
    string OtherPartyName,
    string ViewerRole,
    int UnreadCount,
    IReadOnlyCollection<ConversationMessageView> Messages);

public sealed record ConversationMessageView(Guid Id, string Body, DateTimeOffset CreatedAt, bool IsMine);
