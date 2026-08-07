namespace Marketplace.Domain.SupplierDiscovery;

public enum SupplierDiscoveryAuditEventType
{
    CandidateCreated,
    RelevanceValidated,
    CandidateRejected,
    InvitationCreated,
    CompanyClaimed
}

public sealed class SupplierDiscoveryAuditEvent
{
    private SupplierDiscoveryAuditEvent() { }

    public Guid Id { get; private set; }
    public Guid CandidateId { get; private set; }
    public Guid ActorId { get; private set; }
    public SupplierDiscoveryAuditEventType Type { get; private set; }
    public Guid? InvitationId { get; private set; }
    public Guid? RfqId { get; private set; }
    public string? Details { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }

    public static SupplierDiscoveryAuditEvent Create(
        Guid candidateId,
        Guid actorId,
        SupplierDiscoveryAuditEventType type,
        DateTimeOffset now,
        Guid? invitationId = null,
        Guid? rfqId = null,
        string? details = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            CandidateId = candidateId,
            ActorId = actorId,
            Type = type,
            InvitationId = invitationId,
            RfqId = rfqId,
            Details = string.IsNullOrWhiteSpace(details) ? null : details.Trim(),
            OccurredAt = now
        };
}
