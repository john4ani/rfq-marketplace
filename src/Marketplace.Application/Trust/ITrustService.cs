namespace Marketplace.Application.Trust;

public interface ITrustService
{
    Task<AccountTrustSummary?> GetForAccountAsync(Guid accountId, CancellationToken cancellationToken);
    Task<TrustFacts?> GetFactsAsync(Guid accountId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<AccountTrustSummary>> SearchAsync(string? search, CancellationToken cancellationToken);
    Task<AccountTrustSummary?> ReviewAsync(Guid administratorId, Guid accountId, TrustReviewInput input, CancellationToken cancellationToken);
}
public sealed record TrustReviewInput(bool PhoneVerified, bool CompanyVerified, string? Note);
public sealed record TrustAuditEventView(string Type, string? Details, DateTimeOffset OccurredAt);
public sealed record AccountTrustSummary(Guid AccountId, string Email, IReadOnlyCollection<string> Roles, bool EmailVerified, bool PhoneVerified, DateTimeOffset? PhoneVerifiedAt, bool CompanyVerified, DateTimeOffset? CompanyVerifiedAt, int ProfileCompletenessPercent, int PublishedRequests, int SubmittedOffers, int SelectedOffers, IReadOnlyCollection<TrustAuditEventView> RecentActivity);
public sealed record TrustFacts(
    bool EmailVerified, bool PhoneVerified, bool CompanyVerified,
    DateTimeOffset MemberSince, int PublishedRequests, int CompletedRequests,
    int SubmittedOffers, int ConfirmedCollaborations)
{
    public IReadOnlyCollection<string> Compact(string role) => role == "Supplier"
        ? new[] { CompanyVerified ? "Companie verificată" : string.Empty, SubmittedOffers > 0 ? $"{SubmittedOffers} oferte trimise" : string.Empty, ConfirmedCollaborations > 0 ? $"{ConfirmedCollaborations} colaborări confirmate" : string.Empty }.Where(item => item.Length > 0).Take(3).ToArray()
        : new[] { EmailVerified ? "E-mail verificat" : string.Empty, PhoneVerified ? "Telefon verificat" : string.Empty, CompletedRequests > 0 ? $"{CompletedRequests} cereri finalizate" : string.Empty }.Where(item => item.Length > 0).Take(3).ToArray();
}
