namespace Marketplace.Domain.Identity;

public static class MarketplaceRoles
{
    public const string Buyer = "Buyer";
    public const string Supplier = "Supplier";
    public const string Administrator = "Administrator";

    public static readonly string[] All = [Buyer, Supplier, Administrator];
}
