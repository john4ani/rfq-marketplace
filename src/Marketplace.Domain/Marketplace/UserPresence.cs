namespace Marketplace.Domain.LiveMarket;

public sealed class UserPresence
{
    private UserPresence() { }
    public Guid UserId { get; private set; }
    public DateTimeOffset LastSeenAt { get; private set; }

    public static UserPresence Create(Guid userId, DateTimeOffset now) =>
        new() { UserId = userId, LastSeenAt = now };

    public void MarkSeen(DateTimeOffset now) => LastSeenAt = now;
    public bool IsOnline(DateTimeOffset now, TimeSpan timeout) => LastSeenAt >= now.Subtract(timeout);
}
