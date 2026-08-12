using Marketplace.Domain.Profiles;

namespace Marketplace.Domain.Tests;

public sealed class CompanyMembershipTests
{
    [Fact]
    public void MembershipCanBeRevokedWithoutChangingItsIdentityOrRole()
    {
        Guid userId = Guid.NewGuid();
        Guid companyId = Guid.NewGuid();
        DateTimeOffset createdAt = new(2026, 8, 12, 14, 0, 0, TimeSpan.Zero);
        CompanyMembership membership = CompanyMembership.Create(userId, companyId, CompanyMembershipRole.Representative, createdAt);

        membership.Revoke(createdAt.AddMinutes(1));

        Assert.False(membership.IsActive);
        Assert.Equal(userId, membership.UserId);
        Assert.Equal(companyId, membership.CompanyId);
        Assert.Equal(CompanyMembershipRole.Representative, membership.Role);
        Assert.Equal(createdAt, membership.CreatedAt);
        Assert.Equal(createdAt.AddMinutes(1), membership.UpdatedAt);
    }
}
