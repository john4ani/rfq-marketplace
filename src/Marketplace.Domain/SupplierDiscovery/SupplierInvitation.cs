namespace Marketplace.Domain.SupplierDiscovery;

public enum SupplierInvitationStatus
{
    Active,
    Claimed,
    Revoked,
    Expired
}

public sealed class SupplierInvitation
{
    private SupplierInvitation() { }

    public Guid Id { get; private set; }
    public Guid CandidateId { get; private set; }
    public Guid RfqId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public Guid? ClaimedBySupplierId { get; private set; }
    public DateTimeOffset? ClaimedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public uint Version { get; private set; }

    public static SupplierInvitation Create(
        Guid id,
        Guid candidateId,
        Guid rfqId,
        string tokenHash,
        Guid createdBy,
        DateTimeOffset now,
        DateTimeOffset expiresAt)
    {
        if (expiresAt <= now)
            throw new SupplierDiscoveryRuleException("Invitația trebuie să expire în viitor.");
        return new SupplierInvitation
        {
            Id = id,
            CandidateId = candidateId,
            RfqId = rfqId,
            TokenHash = tokenHash,
            CreatedBy = createdBy,
            CreatedAt = now,
            ExpiresAt = expiresAt
        };
    }

    public SupplierInvitationStatus StatusAt(DateTimeOffset now) =>
        ClaimedAt.HasValue ? SupplierInvitationStatus.Claimed
        : RevokedAt.HasValue ? SupplierInvitationStatus.Revoked
        : ExpiresAt <= now ? SupplierInvitationStatus.Expired
        : SupplierInvitationStatus.Active;

    public void Claim(Guid supplierId, DateTimeOffset now)
    {
        if (StatusAt(now) != SupplierInvitationStatus.Active)
            throw new SupplierDiscoveryRuleException("Invitația nu mai este disponibilă.");
        ClaimedBySupplierId = supplierId;
        ClaimedAt = now;
    }

    public void Revoke(DateTimeOffset now)
    {
        if (StatusAt(now) != SupplierInvitationStatus.Active)
            throw new SupplierDiscoveryRuleException("Doar o invitație activă poate fi revocată.");
        RevokedAt = now;
    }
}
