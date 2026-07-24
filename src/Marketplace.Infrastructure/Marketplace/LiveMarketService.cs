using Marketplace.Application.LiveMarket;
using Marketplace.Domain.LiveMarket;
using Marketplace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Infrastructure.LiveMarket;

public sealed class LiveMarketService(MarketplaceDbContext dbContext, TimeProvider timeProvider) : ILiveMarketService
{
    private static readonly TimeSpan OnlineTimeout = TimeSpan.FromMinutes(2);

    public async Task<LiveMarketSnapshot> GetSnapshotAsync(Guid? viewerId, string? activeProfileType, CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        DateTimeOffset onlineSince = now.Subtract(OnlineTimeout);
        LiveMarketParticipant[] buyers = await (
            from profile in dbContext.BuyerProfiles.AsNoTracking()
            join presence in dbContext.UserPresences.AsNoTracking() on profile.Id equals presence.UserId into presences
            from presence in presences.DefaultIfEmpty()
            select new LiveMarketParticipant(profile.Id, profile.DisplayName, "Buyer", presence != null && presence.LastSeenAt >= onlineSince, profile.DefaultCity, profile.DefaultCounty, false))
            .ToArrayAsync(cancellationToken);
        LiveMarketParticipant[] suppliers = await (
            from profile in dbContext.SupplierProfiles.AsNoTracking().Where(profile => profile.IsActive)
            join presence in dbContext.UserPresences.AsNoTracking() on profile.Id equals presence.UserId into presences
            from presence in presences.DefaultIfEmpty()
            select new LiveMarketParticipant(profile.Id, profile.TradingName, "Supplier", presence != null && presence.LastSeenAt >= onlineSince, profile.City, profile.County, false))
            .ToArrayAsync(cancellationToken);
        MarketOrigin? origin = await GetOriginAsync(viewerId, activeProfileType, cancellationToken);
        IEnumerable<LiveMarketParticipant> visible = buyers.Concat(suppliers);
        if (origin is not null)
        {
            visible = visible
                .Where(participant => string.Equals(participant.County, origin.County, StringComparison.OrdinalIgnoreCase))
                .Select(participant => participant with
                {
                    IsSameCity = string.Equals(participant.City, origin.City, StringComparison.OrdinalIgnoreCase)
                });
        }
        LiveMarketParticipant[] participants = visible
            .OrderByDescending(participant => participant.IsSameCity)
            .ThenByDescending(participant => participant.IsOnline)
            .ThenBy(participant => participant.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        return new(now, participants, origin is not null, origin?.City, origin?.County, origin?.RadiusKm);
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

    private async Task<MarketOrigin?> GetOriginAsync(Guid? viewerId, string? activeProfileType, CancellationToken cancellationToken)
    {
        if (!viewerId.HasValue) return null;
        if (string.Equals(activeProfileType, "Buyer", StringComparison.OrdinalIgnoreCase))
        {
            return await dbContext.BuyerProfiles.AsNoTracking()
                .Where(profile => profile.Id == viewerId.Value)
                .Select(profile => new MarketOrigin(profile.DefaultCity, profile.DefaultCounty, profile.MarketRadiusKm))
                .SingleOrDefaultAsync(cancellationToken);
        }
        if (!string.Equals(activeProfileType, "Supplier", StringComparison.OrdinalIgnoreCase)) return null;
        var supplier = await dbContext.SupplierProfiles.AsNoTracking()
            .Where(profile => profile.Id == viewerId.Value)
            .Select(profile => new { profile.City, profile.County })
            .SingleOrDefaultAsync(cancellationToken);
        if (supplier is null) return null;
        int radius = await dbContext.SupplierDeliverySettings.AsNoTracking()
            .Where(settings => settings.SupplierId == viewerId.Value)
            .Select(settings => settings.MaximumDeliveryRadiusKm)
            .SingleOrDefaultAsync(cancellationToken);
        return new(supplier.City, supplier.County, radius > 0 ? radius : 100);
    }

    private sealed record MarketOrigin(string City, string County, int RadiusKm);
}
