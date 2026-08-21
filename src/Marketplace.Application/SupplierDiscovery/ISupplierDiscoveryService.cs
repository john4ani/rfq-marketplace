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
        Guid actorId,
        Guid companyId,
        string token,
        CancellationToken cancellationToken);
    Task<DiscoveryNeedReview?> GetNeedReviewAsync(Guid rfqId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<OutreachOpportunity>> GetApprovedOutreachOpportunitiesAsync(Guid? candidateId, Guid? rfqId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<AcquisitionCandidate>> GetAcquisitionCandidatesAsync(string? county, Guid? categoryId, CancellationToken cancellationToken);
    Task<OpportunityMatchView?> ReviewOpportunityMatchAsync(Guid administratorId, Guid matchId, bool approved, string? note, CancellationToken cancellationToken);
    Task<OpportunityMatchView?> CreateCandidateMatchAsync(Guid administratorId, Guid rfqId, Guid candidateId, CancellationToken cancellationToken);
    Task<SupplierOutreachResult?> SendEmailInvitationAsync(Guid administratorId, Guid candidateId, Guid opportunityMatchId, CancellationToken cancellationToken);
    Task<SupplierOutreachResult?> RecordManualContactAsync(Guid administratorId, Guid candidateId, Guid opportunityMatchId, SupplierOutreachChannel channel, string? note, CancellationToken cancellationToken);
    Task<SupplierOutreachResult?> SuppressContactAsync(Guid administratorId, Guid candidateId, string? reason, CancellationToken cancellationToken);
    Task<bool> OptOutEmailAsync(string token, CancellationToken cancellationToken);
    Task<SupplierWebDiscoveryRunView?> DiscoverOnWebAsync(Guid administratorId, Guid rfqId, CancellationToken cancellationToken);
    Task<SupplierWebDiscoveryRunView> DiscoverCompanyOnWebAsync(Guid administratorId, SupplierCompanyWebSearchInput input, CancellationToken cancellationToken);
    Task<SupplierWebDiscoveryRunView?> QualifyWebDiscoveryAsync(Guid administratorId, Guid runId, CancellationToken cancellationToken);
    Task<SupplierWebCandidateAccepted?> AcceptWebCandidateAsync(Guid administratorId, Guid runId, int candidateIndex, IReadOnlyCollection<Guid>? categoryIds, CancellationToken cancellationToken);
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

public sealed record OpportunityMatchView(Guid Id, Guid RfqId, Guid? CompanyId, Guid? CandidateId, string CompanyName, string? City, string? County, string MatchReason, OpportunityMatchStatus Status, DateTimeOffset MatchedAt, string? ReviewNote, int RelevantOpportunityCount, int ActiveOpportunityCount, int ApprovedOpportunityCount, int RejectedOpportunityCount, DateTimeOffset? LastOpportunityAt, bool Claimed, int InvitationCount, int RecentOpportunityCount, string? ContactEmail, string? PhoneNumber, string? Website, DateTimeOffset? LastContactAt, SupplierOutreachStatus? LastContactStatus, bool ContactSuppressed, bool EmailEligible, string? EmailUnavailableReason);
public sealed record DiscoveryNeedReview(
    Guid RfqId,
    string Title,
    string DeliveryLocality,
    string County,
    DateTimeOffset QuotationDeadline,
    DiscoveryNeedRequestContext Request,
    DiscoveryNeedBuyerContext Buyer,
    IReadOnlyCollection<OpportunityMatchView> RegisteredCompanies,
    IReadOnlyCollection<OpportunityMatchView> Candidates);
public sealed record DiscoveryNeedRequestContext(
    string? Description,
    string CategoryName,
    string DeliveryAddress,
    DateOnly? PreferredDeliveryDate,
    bool DeliveryRequired,
    decimal? EstimatedBudget,
    bool AlternativeProductsAccepted,
    string? BuyerNotes,
    IReadOnlyCollection<DiscoveryNeedLine> Lines);
public sealed record DiscoveryNeedLine(string Name, string? Specification, decimal Quantity, Marketplace.Domain.Rfqs.UnitOfMeasure Unit, Marketplace.Domain.Rfqs.RequirementKind Kind);
public sealed record DiscoveryNeedBuyerContext(string DisplayName, string? CompanyName, string? Email, string? PhoneNumber, string Locality, string County);
public sealed record OutreachOpportunity(OpportunityMatchView Match, string RfqTitle, string DeliveryLocality, string County, DateTimeOffset QuotationDeadline);
public sealed record AcquisitionCandidate(Guid CandidateId, string CompanyName, string City, string County, IReadOnlyCollection<SupplierCandidateCategoryView> Categories, int RelevantOpportunityCount, int ActiveOpportunityCount, int ApprovedOpportunityCount, int RejectedOpportunityCount, DateTimeOffset? LastOpportunityAt, int InvitationCount, bool Claimed, int RecentOpportunityCount);

public sealed record SupplierOutreachResult(
    Guid CandidateId,
    Guid OpportunityMatchId,
    SupplierOutreachChannel Channel,
    SupplierOutreachStatus Status,
    bool Eligible,
    string Message,
    DateTimeOffset? CooldownUntil = null,
    Guid? InvitationId = null);

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

public sealed record SupplierCompanyWebSearchInput(string CompanyName, string? Locality, string? County, IReadOnlyCollection<Guid> CategoryIds);
public sealed record SupplierWebDiscoveryRunView(Guid Id, Guid? RfqId, string Status, string? FailureMessage, int WebSearchCallCount, IReadOnlyCollection<SupplierWebDiscoverySourceView> Sources, IReadOnlyCollection<SupplierWebEvidenceView> ProvisionalEvidence, IReadOnlyCollection<SupplierWebCandidateView> Candidates, IReadOnlyCollection<SupplierWebRejectedResult> RejectedResults);
public sealed record SupplierWebDiscoverySourceView(string Source, int ResultCount, string? Warning);
public sealed record SupplierWebEvidenceView(
    string CompanyName,
    string Locality,
    string County,
    IReadOnlyCollection<string> Sources,
    IReadOnlyCollection<string> SourceUrls,
    double? Latitude = null,
    double? Longitude = null,
    decimal? DistanceKm = null);
public sealed record SupplierWebCandidateView(string CompanyName, string Locality, string County, string? Website, string? Phone, string? Email, string ActivityEvidence, string? CaenCode, string ProductEvidence, string GeographicRelevance, string Confidence, IReadOnlyCollection<string> SourceUrls, IReadOnlyCollection<string> Sources, string WhyRelevant);
public sealed record SupplierWebRejectedResult(string CompanyName, string Reason, IReadOnlyCollection<string> SourceUrls);
public sealed record SupplierWebCandidateAccepted(SupplierCandidateView Candidate, OpportunityMatchView? OpportunityMatch, bool ReusedExistingCandidate);

public sealed class SupplierCandidateConflictException(string message) : InvalidOperationException(message);
public sealed class SupplierDiscoveryConcurrencyException(string message, Exception innerException)
    : InvalidOperationException(message, innerException);
