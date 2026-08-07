namespace Marketplace.Domain.Rfqs;

public enum UnitOfMeasure { Piece, Pack, Bag, Kilogram, Tonne, Metre, SquareMetre, CubicMetre, Litre, Pallet, Other }

public sealed class RfqItem
{
    private RfqItem() { }
    public Guid Id { get; private set; }
    public Guid RfqId { get; private set; }
    public int ItemNumber { get; private set; }
    public Guid NeedCategoryId { get; private set; }
    public NeedCategory NeedCategory { get; private set; } = null!;
    public Guid CategoryId => NeedCategory.CategoryId;
    public RequirementKind Kind { get; private set; }
    public string ProductName { get; private set; } = string.Empty;
    public string? ManufacturerOrBrand { get; private set; }
    public string? Specification { get; private set; }
    public decimal Quantity { get; private set; }
    public UnitOfMeasure Unit { get; private set; }
    public bool AlternativesAccepted { get; private set; }
    public string? BuyerNotes { get; private set; }

    public static RfqItem Create(Guid id, Guid rfqId, int itemNumber, RfqItemDraft draft, NeedCategory needCategory)
    {
        var item = new RfqItem { Id = id, RfqId = rfqId };
        item.Update(itemNumber, draft, needCategory);
        return item;
    }

    internal void Update(int itemNumber, RfqItemDraft draft, NeedCategory needCategory)
    {
        if (string.IsNullOrWhiteSpace(draft.ProductName)) throw new RfqRuleException($"Denumirea produsului sau serviciului de la poziția {itemNumber} este obligatorie.");
        if (draft.Quantity <= 0) throw new RfqRuleException($"Cantitatea de la poziția {itemNumber} trebuie să fie mai mare decât zero.");
        if (!Enum.IsDefined(draft.Kind)) throw new RfqRuleException($"Tipul poziției {itemNumber} nu este valid.");
        ItemNumber = itemNumber;
        NeedCategoryId = needCategory.Id;
        NeedCategory = needCategory;
        Kind = draft.Kind;
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
    Guid? CategoryId = null,
    RequirementKind Kind = RequirementKind.Product);
