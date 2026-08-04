namespace Marketplace.Domain.Rfqs;

public enum ProcurementNeedSource { Manual, Assisted, Imported, Administrator }
public enum ProcurementInputType { StructuredForm, FreeText, ShoppingList, ProjectDescription }
public enum ProcurementNeedStatus { Draft, Published, ReceivingResponses, Awarded, Completed, Cancelled }
public enum RequirementKind { Product, Service }
public enum ProcurementOutcome
{
    PurchasedThroughCompaSpot,
    PurchasedFromParticipant,
    PurchasedElsewhere,
    Postponed,
    Cancelled,
    NoSuitableOffer,
    InsufficientResponses,
    Other
}

public sealed record ProjectContext(string? Name, string? Description)
{
    public bool IsEmpty => string.IsNullOrWhiteSpace(Name) && string.IsNullOrWhiteSpace(Description);
}

public sealed record DeliveryConstraints(
    string Address,
    string Locality,
    string County,
    int SupplierSearchRadiusKm,
    decimal? Latitude,
    decimal? Longitude,
    DateOnly? PreferredDeliveryDate,
    bool TransportRequired);

public enum ProcurementNeedAuditEventType
{
    Created,
    Updated,
    Published,
    OfferReceived,
    DeadlineExtended,
    OfferSelected,
    Reopened,
    DeliveryReported,
    Completed,
    Cancelled,
    Migrated
}

public sealed class ProcurementNeedAuditEvent
{
    private ProcurementNeedAuditEvent() { }

    public Guid Id { get; private set; }
    public Guid ProcurementNeedId { get; private set; }
    public Guid ActorId { get; private set; }
    public ProcurementNeedAuditEventType Type { get; private set; }
    public ProcurementNeedStatus FromStatus { get; private set; }
    public ProcurementNeedStatus ToStatus { get; private set; }
    public string? Details { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }

    public static ProcurementNeedAuditEvent Create(
        Guid id,
        Guid procurementNeedId,
        Guid actorId,
        ProcurementNeedAuditEventType type,
        ProcurementNeedStatus fromStatus,
        ProcurementNeedStatus toStatus,
        string? details,
        DateTimeOffset occurredAt)
    {
        if (id == Guid.Empty || procurementNeedId == Guid.Empty || actorId == Guid.Empty)
            throw new ArgumentException("Audit event identifiers are required.");
        string? cleanDetails = string.IsNullOrWhiteSpace(details) ? null : details.Trim();
        if (cleanDetails?.Length > 1000)
            throw new ArgumentException("Audit event details cannot exceed 1,000 characters.", nameof(details));

        return new ProcurementNeedAuditEvent
        {
            Id = id,
            ProcurementNeedId = procurementNeedId,
            ActorId = actorId,
            Type = type,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            Details = cleanDetails,
            OccurredAt = occurredAt
        };
    }
}
