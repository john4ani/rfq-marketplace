namespace Marketplace.Domain.SupplierDiscovery;

public enum OpportunityMatchSource
{
    RegisteredProviderMatching,
    ExistingCandidateMatching,
    CandidateDiscovery
}

public enum OpportunityMatchStatus
{
    RegisteredCompany,
    PendingReview,
    Approved,
    Rejected
}

/// <summary>Historical, opportunity-specific relevance of exactly one commercial target.</summary>
public sealed class OpportunityMatch
{
    private OpportunityMatch() { }

    public Guid Id { get; private set; }
    public Guid RfqId { get; private set; }
    public Guid? SupplierProfileId { get; private set; }
    public Guid? SupplierCandidateId { get; private set; }
    public OpportunityMatchSource Source { get; private set; }
    public OpportunityMatchStatus Status { get; private set; }
    public string MatchReason { get; private set; } = string.Empty;
    public DateTimeOffset MatchedAt { get; private set; }
    public Guid? AdminReviewedBy { get; private set; }
    public DateTimeOffset? AdminReviewedAt { get; private set; }
    public string? AdminReviewNote { get; private set; }
    public uint Version { get; private set; }

    public static OpportunityMatch ForCompany(Guid rfqId, Guid companyId, string reason, DateTimeOffset now) =>
        Create(rfqId, companyId, null, OpportunityMatchSource.RegisteredProviderMatching, OpportunityMatchStatus.RegisteredCompany, reason, now);

    public static OpportunityMatch ForCandidate(Guid rfqId, Guid candidateId, OpportunityMatchSource source, string reason, DateTimeOffset now) =>
        Create(rfqId, null, candidateId, source, OpportunityMatchStatus.PendingReview, reason, now);

    public void Review(Guid administratorId, bool approved, string? note, DateTimeOffset now)
    {
        if (Status == OpportunityMatchStatus.RegisteredCompany)
            throw new SupplierDiscoveryRuleException("Potrivirea unei firme înregistrate nu necesită aprobare de contact.");
        if (Status == (approved ? OpportunityMatchStatus.Approved : OpportunityMatchStatus.Rejected)) return;
        Status = approved ? OpportunityMatchStatus.Approved : OpportunityMatchStatus.Rejected;
        AdminReviewedBy = administratorId;
        AdminReviewedAt = now;
        AdminReviewNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }

    private static OpportunityMatch Create(Guid rfqId, Guid? companyId, Guid? candidateId, OpportunityMatchSource source, OpportunityMatchStatus status, string reason, DateTimeOffset now)
    {
        if ((companyId.HasValue ? 1 : 0) + (candidateId.HasValue ? 1 : 0) != 1)
            throw new SupplierDiscoveryRuleException("O potrivire trebuie să aibă exact o firmă sau un candidat.");
        if (string.IsNullOrWhiteSpace(reason)) throw new SupplierDiscoveryRuleException("Motivul potrivirii este obligatoriu.");
        return new() { Id = Guid.NewGuid(), RfqId = rfqId, SupplierProfileId = companyId, SupplierCandidateId = candidateId,
            Source = source, Status = status, MatchReason = reason.Trim(), MatchedAt = now };
    }
}
