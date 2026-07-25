namespace Marketplace.Domain.Notifications;

public enum NotificationType
{
    OfferReceived,
    OfferRevised,
    RequestReopened,
    MessageReceived,
    DeliveryReported,
    DeliveryConfirmed
}

public sealed class Notification
{
    private Notification() { }
    public Guid Id { get; private set; }
    public Guid RecipientId { get; private set; }
    public NotificationType Type { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public string Link { get; private set; } = string.Empty;
    public string DeduplicationKey { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ReadAt { get; private set; }
    public bool IsRead => ReadAt.HasValue;

    public static Notification Create(Guid recipientId, NotificationType type, string title, string message, string link, string deduplicationKey, DateTimeOffset now) =>
        new() { Id = Guid.NewGuid(), RecipientId = recipientId, Type = type, Title = title, Message = message,
            Link = link, DeduplicationKey = deduplicationKey, CreatedAt = now };

    public void MarkRead(DateTimeOffset now) => ReadAt ??= now;
}
