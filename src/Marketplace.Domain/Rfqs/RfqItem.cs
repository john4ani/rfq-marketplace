namespace Marketplace.Domain.Rfqs;

public enum UnitOfMeasure { Piece, Pack, Bag, Kilogram, Tonne, Metre, SquareMetre, CubicMetre, Litre, Pallet, Other }

public sealed class RfqItem
{
    private RfqItem() { }
    public Guid Id { get; private set; }
    public Guid RfqId { get; private set; }
    public int ItemNumber { get; private set; }
    public Guid? CategoryId { get; private set; }
    public string ProductName { get; private set; } = string.Empty;
    public string? ManufacturerOrBrand { get; private set; }
    public string? Specification { get; private set; }
    public decimal Quantity { get; private set; }
    public UnitOfMeasure Unit { get; private set; }
    public bool AlternativesAccepted { get; private set; }
    public string? BuyerNotes { get; private set; }

    public static RfqItem Create(Guid id, Guid rfqId, int itemNumber, RfqItemDraft draft)
    {
        var item = new RfqItem { Id = id, RfqId = rfqId };
        item.Update(itemNumber, draft);
        return item;
    }

    internal void Update(int itemNumber, RfqItemDraft draft)
    {
        if (string.IsNullOrWhiteSpace(draft.ProductName)) throw new RfqRuleException($"Denumirea materialului de la poziția {itemNumber} este obligatorie.");
        if (draft.Quantity <= 0) throw new RfqRuleException($"Cantitatea de la poziția {itemNumber} trebuie să fie mai mare decât zero.");
        ItemNumber = itemNumber;
        CategoryId = draft.CategoryId;
        ProductName = draft.ProductName.Trim();
        ManufacturerOrBrand = Clean(draft.ManufacturerOrBrand);
        Specification = Clean(draft.Specification);
        Quantity = draft.Quantity;
        Unit = draft.Unit;
        AlternativesAccepted = draft.AlternativesAccepted;
        BuyerNotes = Clean(draft.BuyerNotes);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record RfqItemDraft(
    string ProductName,
    string? ManufacturerOrBrand,
    string? Specification,
    decimal Quantity,
    UnitOfMeasure Unit,
    bool AlternativesAccepted,
    string? BuyerNotes,
    Guid? CategoryId = null);
