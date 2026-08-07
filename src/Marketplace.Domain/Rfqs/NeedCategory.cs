namespace Marketplace.Domain.Rfqs;

public sealed class NeedCategory
{
    private NeedCategory() { }

    public Guid Id { get; private set; }
    public Guid ProcurementNeedId { get; private set; }
    public Guid CategoryId { get; private set; }
    public int SortOrder { get; private set; }

    internal static NeedCategory Create(Guid procurementNeedId, Guid categoryId, int sortOrder)
    {
        if (procurementNeedId == Guid.Empty) throw new RfqRuleException("Cererea este obligatorie.");
        if (categoryId == Guid.Empty) throw new RfqRuleException("Categoria necesarului este obligatorie.");
        return new NeedCategory
        {
            Id = Guid.NewGuid(),
            ProcurementNeedId = procurementNeedId,
            CategoryId = categoryId,
            SortOrder = sortOrder
        };
    }

    internal void Reorder(int sortOrder) => SortOrder = sortOrder;
}
