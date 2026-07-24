namespace Marketplace.Domain.Suppliers;

public sealed class SupplierDeliveryArea
{
    private SupplierDeliveryArea() { }
    public Guid SupplierId { get; private set; }
    public string County { get; private set; } = string.Empty;

    public static SupplierDeliveryArea Create(Guid supplierId, string county) =>
        new() { SupplierId = supplierId, County = county };
}
