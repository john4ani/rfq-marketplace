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

    [Fact]
    public void UnavailablePositionsAreExcludedFromTheOfferTotal()
    {
        Offer offer = Offer.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new OfferDraft(
                OfferPricingMode.Itemized, null, 30, 4,
                new DateOnly(2026, 8, 1), null, null,
                [
                    new OfferItemDraft(Guid.NewGuid(), 2, 100, null, true),
                    new OfferItemDraft(Guid.NewGuid(), 1, 999, null, false)
                ]),
            Now);

        Assert.Equal(200, offer.ItemsTotal);
        Assert.Equal(230, offer.Total);
    }

    [Fact]
    public void VatExcludedIsAddedToTheCommercialTotal()
    {
        Offer offer = Offer.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new OfferDraft(
                OfferPricingMode.Itemized, null, 30, 4,
                new DateOnly(2026, 8, 1), null, null,
                [new OfferItemDraft(Guid.NewGuid(), 2, 100, "Produs ofertat", true)],
                Coverage: OfferCoverage.Full,
                TaxTreatment: OfferTaxTreatment.VatExcluded,
                VatRate: 19,
                DeliveryTerms: OfferDeliveryTerms.AddedToTotal),
            Now);

        Assert.Equal(230, offer.NetTotal);
        Assert.Equal(43.70m, offer.VatAmount);
        Assert.Equal(273.70m, offer.Total);
        Assert.True(offer.HasComparableTotal);
    }

    [Fact]
    public void AlternativeRequiresAConcreteDescription()
    {
        OfferRuleException error = Assert.Throws<OfferRuleException>(() => Offer.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new OfferDraft(
                OfferPricingMode.Itemized, null, 0, 2,
                new DateOnly(2026, 8, 1), null, null,
                [new OfferItemDraft(Guid.NewGuid(), 1, 10, null, true, true)]),
            Now));

        Assert.Contains("alternativ", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OfferCannotBeSubmittedWithoutExplicitTaxTreatment()
    {
        Offer offer = Offer.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new OfferDraft(
                OfferPricingMode.Itemized, null, 0, 2,
                new DateOnly(2026, 8, 1), null, null,
                [new OfferItemDraft(Guid.NewGuid(), 1, 10, null, true)]),
            Now);

        OfferRuleException error = Assert.Throws<OfferRuleException>(() => offer.Submit(Now));

        Assert.Contains("TVA", error.Message, StringComparison.Ordinal);
    }
}
