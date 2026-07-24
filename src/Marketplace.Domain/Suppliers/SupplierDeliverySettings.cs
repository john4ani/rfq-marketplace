namespace Marketplace.Domain.Suppliers;

public sealed class SupplierDeliverySettings
{
    private SupplierDeliverySettings() { }
    public Guid SupplierId { get; private set; }
    public int NormalDeliveryRadiusKm { get; private set; }
    public int MaximumDeliveryRadiusKm { get; private set; }
    public decimal? MinimumOrderValue { get; private set; }
    public bool NationwideDelivery { get; private set; }
    public bool CanFulfillMultiCategoryRequests { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static SupplierDeliverySettings Create(
        Guid supplierId,
        int normalRadius,
        int maximumRadius,
        decimal? minimumOrderValue,
        bool nationwide,
        bool canFulfillMultiCategoryRequests,
        DateTimeOffset now)
    {
        Validate(normalRadius, maximumRadius, minimumOrderValue);
        return new()
        {
            SupplierId = supplierId,
            NormalDeliveryRadiusKm = normalRadius,
            MaximumDeliveryRadiusKm = maximumRadius,
            MinimumOrderValue = minimumOrderValue,
            NationwideDelivery = nationwide,
            CanFulfillMultiCategoryRequests = canFulfillMultiCategoryRequests,
            UpdatedAt = now
        };
    }

    public void Update(
        int normalRadius,
        int maximumRadius,
        decimal? minimumOrderValue,
        bool nationwide,
        bool canFulfillMultiCategoryRequests,
        DateTimeOffset now)
    {
        Validate(normalRadius, maximumRadius, minimumOrderValue);
        NormalDeliveryRadiusKm = normalRadius;
        MaximumDeliveryRadiusKm = maximumRadius;
        MinimumOrderValue = minimumOrderValue;
        NationwideDelivery = nationwide;
        CanFulfillMultiCategoryRequests = canFulfillMultiCategoryRequests;
        UpdatedAt = now;
    }

    private static void Validate(int normalRadius, int maximumRadius, decimal? minimumOrderValue)
    {
        if (normalRadius is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(normalRadius));
        if (maximumRadius < normalRadius || maximumRadius > 1000) throw new ArgumentOutOfRangeException(nameof(maximumRadius));
        if (minimumOrderValue < 0) throw new ArgumentOutOfRangeException(nameof(minimumOrderValue));
    }
}
