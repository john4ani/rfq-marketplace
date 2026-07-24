using Marketplace.Application.LiveMarket;
using Marketplace.Domain.LiveMarket;
using Marketplace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Infrastructure.LiveMarket;

public sealed class LiveMarketService(MarketplaceDbContext dbContext, TimeProvider timeProvider) : ILiveMarketService
{
    private static readonly TimeSpan OnlineTimeout = TimeSpan.FromMinutes(2);

    public async Task<LiveMarketSnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        DateTimeOffset onlineSince = now.Subtract(OnlineTimeout);
        LiveMarketParticipant[] buyers = await (
            from profile in dbContext.BuyerProfiles.AsNoTracking()
            join presence in dbContext.UserPresences.AsNoTracking() on profile.Id equals presence.UserId into presences
            from presence in presences.DefaultIfEmpty()
            select new LiveMarketParticipant(profile.Id, profile.DisplayName, "Buyer", presence != null && presence.LastSeenAt >= onlineSince))
            .ToArrayAsync(cancellationToken);
        LiveMarketParticipant[] suppliers = await (
            from profile in dbContext.SupplierProfiles.AsNoTracking().Where(profile => profile.IsActive)
            join presence in dbContext.UserPresences.AsNoTracking() on profile.Id equals presence.UserId into presences
            from presence in presences.DefaultIfEmpty()
            select new LiveMarketParticipant(profile.Id, profile.TradingName, "Supplier", presence != null && presence.LastSeenAt >= onlineSince))
            .ToArrayAsync(cancellationToken);
        LiveMarketParticipant[] participants = buyers.Concat(suppliers)
            .OrderByDescending(participant => participant.IsOnline)
            .ThenBy(participant => participant.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        return new(now, participants);
    }

    public async Task MarkOnlineAsync(Guid userId, CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO "UserPresences" ("UserId", "LastSeenAt")
             VALUES ({userId}, {now})
             ON CONFLICT ("UserId")
             DO UPDATE SET "LastSeenAt" = EXCLUDED."LastSeenAt";
             """,
            cancellationToken);
    }

    public async Task MarkOfflineAsync(Guid userId, CancellationToken cancellationToken)
    {
        UserPresence? presence = await dbContext.UserPresences.SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        if (presence is null) return;
        dbContext.UserPresences.Remove(presence);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
