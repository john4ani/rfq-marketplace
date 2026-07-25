using Marketplace.Domain.Offers;

namespace Marketplace.Domain.Tests;

public sealed class OfferTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 25, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ItemizedOfferWithoutPositionsCannotBeSubmittedAsDeliveryCost()
    {
        Offer offer = Offer.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            new OfferDraft(
                OfferPricingMode.Itemized,
                null,
                1_250,
                4,
                new DateOnly(2026, 8, 1),
                null,
                null,
                []),
            Now);

        OfferRuleException error = Assert.Throws<OfferRuleException>(() => offer.Submit(Now.AddMinutes(1)));

        Assert.Contains("preț total", error.Message, StringComparison.Ordinal);
    }
}
