namespace Marketplace.Domain.SupplierDiscovery;

public enum SupplierOutreachChannel
{
    Email,
    Phone,
    InPerson,
    Sms
}

public enum SupplierOutreachStatus
{
    Pending,
    Sent,
    Failed,
    Recorded
}

public enum SupplierContactSuppressionScope
{
    Email,
    AllExternalContact
}

/// <summary>Audit record for a human-approved candidate contact attempt. It never stores message content or tokens.</summary>
public sealed class SupplierOutreachContact
{
    private SupplierOutreachContact() { }

    public Guid Id { get; private set; }
    public Guid CandidateId { get; private set; }
    public Guid OpportunityMatchId { get; private set; }
    public Guid? InvitationId { get; private set; }
    public SupplierOutreachChannel Channel { get; private set; }
    public SupplierOutreachStatus Status { get; private set; }
    public string? Note { get; private set; }
    public string? OptOutTokenHash { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? AttemptedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public string? FailureReason { get; private set; }
    public uint Version { get; private set; }

    public static SupplierOutreachContact StartEmail(Guid candidateId, Guid matchId, Guid invitationId, string optOutTokenHash, Guid administratorId, DateTimeOffset now) =>
        new() { Id = Guid.NewGuid(), CandidateId = candidateId, OpportunityMatchId = matchId, InvitationId = invitationId, Channel = SupplierOutreachChannel.Email, Status = SupplierOutreachStatus.Pending, OptOutTokenHash = optOutTokenHash, CreatedBy = administratorId, CreatedAt = now, AttemptedAt = now };

    public static SupplierOutreachContact Record(Guid candidateId, Guid matchId, SupplierOutreachChannel channel, string? note, Guid administratorId, DateTimeOffset now)
    {
        if (channel is not (SupplierOutreachChannel.Phone or SupplierOutreachChannel.InPerson))
            throw new SupplierDiscoveryRuleException("Contactul manual poate fi înregistrat doar pentru telefon sau contact direct.");
        return new() { Id = Guid.NewGuid(), CandidateId = candidateId, OpportunityMatchId = matchId, Channel = channel, Status = SupplierOutreachStatus.Recorded, Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(), CreatedBy = administratorId, CreatedAt = now, AttemptedAt = now, CompletedAt = now };
    }

    public void MarkSent(DateTimeOffset now) { Status = SupplierOutreachStatus.Sent; CompletedAt = now; FailureReason = null; }
    public void MarkFailed(string reason, DateTimeOffset now) { Status = SupplierOutreachStatus.Failed; CompletedAt = now; FailureReason = reason; }
}

/// <summary>Candidate-level contact suppression. A later rediscovery must consult this record before outreach.</summary>
public sealed class SupplierContactSuppression
{
    private SupplierContactSuppression() { }

    public Guid Id { get; private set; }
    public Guid CandidateId { get; private set; }
    public SupplierContactSuppressionScope Scope { get; private set; }
    public string? NormalizedEmail { get; private set; }
    public string? Reason { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public uint Version { get; private set; }

    public static SupplierContactSuppression Create(Guid candidateId, SupplierContactSuppressionScope scope, string? email, string? reason, Guid? createdBy, DateTimeOffset now) =>
        new() { Id = Guid.NewGuid(), CandidateId = candidateId, Scope = scope, NormalizedEmail = NormalizeEmail(email), Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(), CreatedBy = createdBy, CreatedAt = now };

    public static string? NormalizeEmail(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
}
