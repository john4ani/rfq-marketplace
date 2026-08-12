namespace Marketplace.Domain.Profiles;

public enum CompanyMembershipRole
{
    Owner,
    Representative
}

/// <summary>
/// Associates a user with the legacy SupplierProfile company record.
/// SupplierProfile keeps its existing shared-key identity for compatibility.
/// </summary>
public sealed class CompanyMembership
{
    private CompanyMembership() { }

    public Guid UserId { get; private set; }
    public Guid CompanyId { get; private set; }
    public CompanyMembershipRole Role { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public bool IsActive => RevokedAt is null;

    public static CompanyMembership Create(
        Guid userId,
        Guid companyId,
        CompanyMembershipRole role,
        DateTimeOffset now) =>
        new()
        {
            UserId = userId,
            CompanyId = companyId,
            Role = role,
            CreatedAt = now,
            UpdatedAt = now
        };

    public void Revoke(DateTimeOffset now)
    {
        if (RevokedAt is not null) return;
        RevokedAt = now;
        UpdatedAt = now;
    }
}
