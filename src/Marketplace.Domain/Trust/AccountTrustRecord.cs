namespace Marketplace.Domain.Trust;

/// <summary>Factual verification facts for an account. A missing timestamp is deliberately not a claim.</summary>
public sealed class AccountTrustRecord
{
    private AccountTrustRecord() { }
    public Guid AccountId { get; private set; }
    public DateTimeOffset? PhoneVerifiedAt { get; private set; }
    public Guid? PhoneVerifiedBy { get; private set; }
    public DateTimeOffset? CompanyVerifiedAt { get; private set; }
    public Guid? CompanyVerifiedBy { get; private set; }
    public string? VerificationNote { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public static AccountTrustRecord Create(Guid accountId, DateTimeOffset now) => new() { AccountId = accountId, UpdatedAt = now };
    public void Review(bool phoneVerified, bool companyVerified, string? note, Guid reviewerId, DateTimeOffset now)
    {
        PhoneVerifiedAt = phoneVerified ? now : null; PhoneVerifiedBy = phoneVerified ? reviewerId : null;
        CompanyVerifiedAt = companyVerified ? now : null; CompanyVerifiedBy = companyVerified ? reviewerId : null;
        VerificationNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim(); UpdatedAt = now;
    }
}
