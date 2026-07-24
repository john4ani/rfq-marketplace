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

    private static RfqDraft Draft(IReadOnlyCollection<RfqItemDraft> items) => new(
        "Materiale renovare", null, null, Guid.NewGuid(), "Strada Test 1", "Iași", "Iași", 30,
        null, null, null, Now.AddDays(2), true, null, null, items);
}
