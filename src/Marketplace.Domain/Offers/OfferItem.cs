namespace Marketplace.Domain.Offers;

public sealed class OfferItem
{
    private OfferItem() { }
    public Guid Id { get; private set; }
    public Guid OfferId { get; private set; }
    public Guid RfqItemId { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public string? ProductDescription { get; private set; }
    public bool Available { get; private set; }
    public static OfferItem Create(Guid id, Guid offerId, OfferItemDraft draft)
    {
        var item = new OfferItem { Id = id, OfferId = offerId };
        item.Update(draft);
        return item;
    }
    internal void Update(OfferItemDraft draft)
    {
        if (draft.RfqItemId == Guid.Empty) throw new OfferRuleException("Poziția cererii este obligatorie.");
        if (draft.Quantity <= 0) throw new OfferRuleException("Cantitatea ofertată trebuie să fie mai mare decât zero.");
        if (draft.UnitPrice < 0) throw new OfferRuleException("Prețul unitar nu poate fi negativ.");
        RfqItemId = draft.RfqItemId; Quantity = draft.Quantity; UnitPrice = draft.UnitPrice;
        ProductDescription = string.IsNullOrWhiteSpace(draft.ProductDescription) ? null : draft.ProductDescription.Trim();
        Available = draft.Available;
    }
}
