using Marketplace.Domain.Rfqs;

namespace Marketplace.Domain.Tests;

public sealed class RfqTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 24, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void RequestCanBePublishedWithoutIndividualMaterials()
    {
        Rfq rfq = Rfq.Create(Guid.NewGuid(), Guid.NewGuid(), Draft([]), Now);
        rfq.Publish(Now);
        Assert.Equal(RfqStatus.Published, rfq.Status);
    }

    [Fact]
    public void PublishedRequestCannotBeEdited()
    {
        RfqDraft draft = Draft([new("Ciment", null, null, 20, UnitOfMeasure.Bag, true, null)]);
        Rfq rfq = Rfq.Create(Guid.NewGuid(), Guid.NewGuid(), draft, Now);
        rfq.Publish(Now);
        Assert.Equal(RfqStatus.Published, rfq.Status);
        Assert.Throws<RfqRuleException>(() => rfq.Update(draft, Now.AddMinutes(1)));
    }

    [Fact]
    public void MaterialQuantityMustBePositive() =>
        Assert.Throws<RfqRuleException>(() => Rfq.Create(Guid.NewGuid(), Guid.NewGuid(),
            Draft([new("Ciment", null, null, 0, UnitOfMeasure.Bag, true, null)]), Now));

    [Fact]
    public void RequestCanBeReopenedOnlyOnce()
    {
        Rfq rfq = Rfq.Create(Guid.NewGuid(), Guid.NewGuid(), Draft([]), Now);
        rfq.Publish(Now);
        rfq.SelectOffer(Guid.NewGuid(), false, Now.AddMinutes(1));
        rfq.Reopen("Oferta a fost selectată din greșeală.", null, Now.AddMinutes(2));
        Assert.Equal(1, rfq.ReopenCount);

        rfq.SelectOffer(Guid.NewGuid(), false, Now.AddMinutes(3));
        RfqRuleException exception = Assert.Throws<RfqRuleException>(() =>
            rfq.Reopen("A doua încercare de redeschidere.", null, Now.AddMinutes(4)));
        Assert.Contains("deja redeschisă", exception.Message);
    }

    [Fact]
    public void PublishedRequestDeadlineCanBeExtended()
    {
        Rfq rfq = Rfq.Create(Guid.NewGuid(), Guid.NewGuid(), Draft([]), Now);
        rfq.Publish(Now);

        rfq.ExtendQuotationDeadline(Now.AddDays(3), Now.AddHours(1));

        Assert.Equal(Now.AddDays(3), rfq.QuotationDeadline);
        Assert.Equal(Now.AddHours(1), rfq.UpdatedAt);
    }

    [Fact]
    public void RequestDeadlineCannotBeShortened()
    {
        Rfq rfq = Rfq.Create(Guid.NewGuid(), Guid.NewGuid(), Draft([]), Now);
        rfq.Publish(Now);

        RfqRuleException exception = Assert.Throws<RfqRuleException>(() =>
            rfq.ExtendQuotationDeadline(Now.AddDays(1), Now.AddHours(1)));

        Assert.Contains("ulterior termenului curent", exception.Message);
    }

    [Fact]
    public void DeliveryIncludedSelectionRequiresProviderReportAndBuyerConfirmation()
    {
        Rfq rfq = Rfq.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Draft([]) with { DeliveryRequired = true },
            Now);
        rfq.Publish(Now);

        rfq.SelectOffer(Guid.NewGuid(), true, Now.AddMinutes(1));
        Assert.Equal(FulfillmentStatus.AwaitingDelivery, rfq.FulfillmentStatus);

        rfq.ReportDelivered(Now.AddDays(1));
        Assert.Equal(FulfillmentStatus.DeliveryReported, rfq.FulfillmentStatus);
        Assert.Equal(RfqStatus.OfferSelected, rfq.Status);

        rfq.ConfirmDelivery(Now.AddDays(1).AddMinutes(1));
        Assert.Equal(FulfillmentStatus.Delivered, rfq.FulfillmentStatus);
        Assert.Equal(RfqStatus.Closed, rfq.Status);
    }

    [Fact]
    public void DeliveryRequiredRequestRejectsPickupOnlyOffer()
    {
        Rfq rfq = Rfq.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Draft([]) with { DeliveryRequired = true },
            Now);
        rfq.Publish(Now);

        RfqRuleException exception = Assert.Throws<RfqRuleException>(
            () => rfq.SelectOffer(Guid.NewGuid(), false, Now.AddMinutes(1)));

        Assert.Contains("transport inclus", exception.Message);
    }

    private static RfqDraft Draft(IReadOnlyCollection<RfqItemDraft> items) => new(
        "Materiale renovare", null, null, Guid.NewGuid(), "Strada Test 1", "Iași", "Iași", 30,
        null, null, null, Now.AddDays(2), true, null, null, items);
}
