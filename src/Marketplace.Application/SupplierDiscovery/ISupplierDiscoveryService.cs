using Marketplace.Domain.SupplierDiscovery;

namespace Marketplace.Application.SupplierDiscovery;

public interface ISupplierDiscoveryService
{
    Task<IReadOnlyCollection<SupplierCandidateView>> GetCandidatesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyCollection<DiscoveryOpportunity>> GetOpenOpportunitiesAsync(CancellationToken cancellationToken);
    Task<SupplierCandidateView> CreateCandidateAsync(
        Guid administratorId,
        SupplierCandidateInput input,
        CancellationToken cancellationToken);
    Task<SupplierCandidateView?> ReviewCandidateAsync(
        Guid administratorId,
        Guid candidateId,
        bool approved,
        string notes,
        CancellationToken cancellationToken);
    Task<SupplierInvitationCreated?> CreateInvitationAsync(
        Guid administratorId,
        Guid candidateId,
        Guid rfqId,
        CancellationToken cancellationToken);
    Task<SupplierInvitationView?> GetInvitationAsync(string token, CancellationToken cancellationToken);
    Task<SupplierInvitationView?> ClaimAsync(
        Guid supplierId,
        string token,
        CancellationToken cancellationToken);
}

public sealed record SupplierCandidateInput(
    string CompanyName,
    string? ContactEmail,
    string? PhoneNumber,
    string? Website,
    string City,
    string County,
    IReadOnlyCollection<Guid> CategoryIds,
    string Source,
    string? Notes);

public sealed record SupplierCandidateView(
    Guid Id,
    string CompanyName,
    string? ContactEmail,
    string? PhoneNumber,
    string? Website,
    string City,
    string County,
    IReadOnlyCollection<SupplierCandidateCategoryView> Categories,
    string Source,
    string? Notes,
    SupplierCandidateStatus Status,
    DateTimeOffset CreatedAt,
    string? ReviewNotes,
    Guid? ClaimedBySupplierId,
    IReadOnlyCollection<SupplierInvitationSummary> Invitations);

public sealed record SupplierCandidateCategoryView(Guid Id, string Name);

public sealed record SupplierInvitationSummary(
    Guid Id,
    Guid RfqId,
    string RfqTitle,
    SupplierInvitationStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt);

public sealed record SupplierInvitationCreated(
    Guid InvitationId,
    string Token,
    DateTimeOffset ExpiresAt);

public sealed record DiscoveryOpportunity(
    Guid Id,
    string Title,
    string DeliveryLocality,
    string County,
    DateTimeOffset QuotationDeadline,
    Guid PrimaryCategoryId,
    IReadOnlyCollection<Guid> RequirementCategoryIds);

public sealed record SupplierInvitationView(
    Guid InvitationId,
    Guid CandidateId,
    string CompanyName,
    string CandidateLabel,
    SupplierInvitationStatus Status,
    DateTimeOffset ExpiresAt,
    Guid RfqId,
    string RfqTitle,
    string? RfqDescription,
    string DeliveryLocality,
    string County,
    DateTimeOffset QuotationDeadline,
    int RequirementCount,
    bool Claimed);

public sealed class SupplierCandidateConflictException(string message) : InvalidOperationException(message);
public sealed class SupplierDiscoveryConcurrencyException(string message, Exception innerException)
    : InvalidOperationException(message, innerException);
