namespace Marketplace.Domain.Trust;

public enum AccountAuditEventType { ProfileSaved, VerificationReviewed }
public sealed class AccountAuditEvent
{
    private AccountAuditEvent() { }
    public Guid Id { get; private set; } public Guid AccountId { get; private set; } public Guid ActorId { get; private set; }
    public AccountAuditEventType Type { get; private set; } public string? Details { get; private set; } public DateTimeOffset OccurredAt { get; private set; }
    public static AccountAuditEvent Create(Guid accountId, Guid actorId, AccountAuditEventType type, string? details, DateTimeOffset now) => new() { Id = Guid.NewGuid(), AccountId = accountId, ActorId = actorId, Type = type, Details = details, OccurredAt = now };
}
