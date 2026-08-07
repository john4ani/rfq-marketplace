namespace Marketplace.Domain.Operations;

/// <summary>A deliberate, reasoned exception to automatic matching for a registered Supplier.</summary>
public sealed class ConciergeMatchOverride
{
    private ConciergeMatchOverride() { }
    public Guid Id { get; private set; }
    public Guid RfqId { get; private set; }
    public Guid SupplierId { get; private set; }
    public Guid AdministratorId { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }

    public static ConciergeMatchOverride Create(Guid rfqId, Guid supplierId, Guid administratorId,
        string reason, DateTimeOffset createdAt)
    {
        string cleanReason = reason?.Trim() ?? string.Empty;
        if (cleanReason.Length is < 10 or > 1000)
            throw new ArgumentException("Reason must contain between 10 and 1,000 characters.", nameof(reason));
        return new() { Id = Guid.NewGuid(), RfqId = rfqId, SupplierId = supplierId,
            AdministratorId = administratorId, Reason = cleanReason, CreatedAt = createdAt };
    }
}
