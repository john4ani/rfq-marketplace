using Marketplace.Domain.LiveMarket;

namespace Marketplace.Domain.Tests;

public sealed class UserPresenceTests
{
    [Fact]
    public void PresenceExpiresAfterTheConfiguredTimeout()
    {
        DateTimeOffset seenAt = new(2026, 7, 24, 8, 0, 0, TimeSpan.Zero);
        UserPresence presence = UserPresence.Create(Guid.NewGuid(), seenAt);
        Assert.True(presence.IsOnline(seenAt.AddSeconds(119), TimeSpan.FromMinutes(2)));
        Assert.False(presence.IsOnline(seenAt.AddSeconds(121), TimeSpan.FromMinutes(2)));
    }
}
