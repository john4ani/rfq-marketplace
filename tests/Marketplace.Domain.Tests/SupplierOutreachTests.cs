using Marketplace.Domain.SupplierDiscovery;

namespace Marketplace.Domain.Tests;

public sealed class SupplierOutreachTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 17, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void EmailOutreachStartsPendingThenTransitionsToSentOrFailed()
    {
        SupplierOutreachContact sent = SupplierOutreachContact.StartEmail(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "HASH", Guid.NewGuid(), Now);
        Assert.Equal(SupplierOutreachStatus.Pending, sent.Status);

        sent.MarkSent(Now.AddSeconds(2));
        Assert.Equal(SupplierOutreachStatus.Sent, sent.Status);
        Assert.Null(sent.FailureReason);

        SupplierOutreachContact failed = SupplierOutreachContact.StartEmail(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "HASH2", Guid.NewGuid(), Now);
        failed.MarkFailed("SMTP delivery failed", Now.AddSeconds(2));
        Assert.Equal(SupplierOutreachStatus.Failed, failed.Status);
        Assert.Equal("SMTP delivery failed", failed.FailureReason);
    }

    [Theory]
    [InlineData(SupplierOutreachChannel.Phone)]
    [InlineData(SupplierOutreachChannel.InPerson)]
    public void ManualContactCanBeRecordedForPhoneOrInPersonOnly(SupplierOutreachChannel channel)
    {
        SupplierOutreachContact contact = SupplierOutreachContact.Record(
            Guid.NewGuid(), Guid.NewGuid(), channel, "Interesat, revine mâine.", Guid.NewGuid(), Now);

        Assert.Equal(SupplierOutreachStatus.Recorded, contact.Status);
        Assert.Equal(channel, contact.Channel);
        Assert.Equal("Interesat, revine mâine.", contact.Note);
    }

    [Theory]
    [InlineData(SupplierOutreachChannel.Email)]
    [InlineData(SupplierOutreachChannel.Sms)]
    public void ManualContactRejectsNonManualChannels(SupplierOutreachChannel channel) =>
        Assert.Throws<SupplierDiscoveryRuleException>(() =>
            SupplierOutreachContact.Record(Guid.NewGuid(), Guid.NewGuid(), channel, null, Guid.NewGuid(), Now));

    [Fact]
    public void SuppressionNormalizesEmailForRediscoveryMatching()
    {
        SupplierContactSuppression suppression = SupplierContactSuppression.Create(
            Guid.NewGuid(), SupplierContactSuppressionScope.Email, "  Contact@Example.TEST  ", "Supplier opt-out", null, Now);

        Assert.Equal("CONTACT@EXAMPLE.TEST", suppression.NormalizedEmail);
        Assert.Equal(SupplierContactSuppressionScope.Email, suppression.Scope);
    }

    [Fact]
    public void SuppressionWithoutEmailNormalizesToNull() =>
        Assert.Null(SupplierContactSuppression.Create(
            Guid.NewGuid(), SupplierContactSuppressionScope.AllExternalContact, null, "Nu dorește contact.", Guid.NewGuid(), Now).NormalizedEmail);
}
