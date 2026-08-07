using Marketplace.Domain.Trust;

namespace Marketplace.Domain.Tests;

public sealed class TrustTests
{
    [Fact]
    public void ReviewRecordsOnlyTheConfirmationsSelectedByTheAdministrator()
    {
        Guid accountId = Guid.NewGuid();
        Guid administratorId = Guid.NewGuid();
        DateTimeOffset now = new(2026, 8, 7, 12, 0, 0, TimeSpan.Zero);
        AccountTrustRecord record = AccountTrustRecord.Create(accountId, now);

        record.Review(phoneVerified: true, companyVerified: false, "Called the listed number.", administratorId, now.AddMinutes(1));

        Assert.Equal(now.AddMinutes(1), record.PhoneVerifiedAt);
        Assert.Equal(administratorId, record.PhoneVerifiedBy);
        Assert.Null(record.CompanyVerifiedAt);
        Assert.Null(record.CompanyVerifiedBy);
        Assert.Equal("Called the listed number.", record.VerificationNote);
    }
}
