namespace Marketplace.Application.LiveMarket;

public interface ILiveMarketService
{
    Task<LiveMarketSnapshot> GetSnapshotAsync(CancellationToken cancellationToken);
    Task MarkOnlineAsync(Guid userId, CancellationToken cancellationToken);
    Task MarkOfflineAsync(Guid userId, CancellationToken cancellationToken);
}

public sealed record LiveMarketSnapshot(DateTimeOffset GeneratedAt, IReadOnlyCollection<LiveMarketParticipant> Participants);
public sealed record LiveMarketParticipant(Guid Id, string Name, string ProfileType, bool IsOnline);
