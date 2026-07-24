namespace Marketplace.Application.LiveMarket;

public interface ILiveMarketService
{
    Task<LiveMarketSnapshot> GetSnapshotAsync(Guid? viewerId, string? activeProfileType, CancellationToken cancellationToken);
    Task MarkOnlineAsync(Guid userId, CancellationToken cancellationToken);
    Task MarkOfflineAsync(Guid userId, CancellationToken cancellationToken);
}

public sealed record LiveMarketSnapshot(
    DateTimeOffset GeneratedAt,
    IReadOnlyCollection<LiveMarketParticipant> Participants,
    bool IsLocationFiltered,
    string? FilterCity,
    string? FilterCounty,
    int? ApproximateRadiusKm);
public sealed record LiveMarketParticipant(
    Guid Id,
    string Name,
    string ProfileType,
    bool IsOnline,
    string City,
    string County,
    bool IsSameCity);
