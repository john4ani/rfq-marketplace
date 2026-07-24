namespace Marketplace.Domain.Suppliers;

public sealed class SupplierCategory
{
    private SupplierCategory() { }
    public Guid SupplierId { get; private set; }
    public Guid CategoryId { get; private set; }

    public static SupplierCategory Create(Guid supplierId, Guid categoryId) =>
        new() { SupplierId = supplierId, CategoryId = categoryId };
}
