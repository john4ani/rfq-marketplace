namespace Marketplace.Domain.Profiles;

public enum BuyerType { Individual, Company }

public sealed class BuyerProfile
{
    private BuyerProfile() { }
    public Guid Id { get; private set; }
    public string DisplayName { get; private set; } = string.Empty;
    public BuyerType BuyerType { get; private set; }
    public string? CompanyName { get; private set; }
    public string? TaxIdentifier { get; private set; }
    public string PhoneNumber { get; private set; } = string.Empty;
    public string DefaultCity { get; private set; } = string.Empty;
    public string DefaultCounty { get; private set; } = string.Empty;
    public int MarketRadiusKm { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static BuyerProfile Create(Guid id, string displayName, BuyerType buyerType, string? companyName, string? taxIdentifier, string phoneNumber, string city, string county, int marketRadiusKm, DateTimeOffset now)
    {
        ValidateRadius(marketRadiusKm);
        return new() { Id = id, DisplayName = displayName, BuyerType = buyerType, CompanyName = companyName, TaxIdentifier = taxIdentifier, PhoneNumber = phoneNumber, DefaultCity = city, DefaultCounty = county, MarketRadiusKm = marketRadiusKm, CreatedAt = now, UpdatedAt = now };
    }

    public void Update(string displayName, BuyerType buyerType, string? companyName, string? taxIdentifier, string phoneNumber, string city, string county, int marketRadiusKm, DateTimeOffset now)
    {
        ValidateRadius(marketRadiusKm);
        DisplayName = displayName; BuyerType = buyerType; CompanyName = companyName; TaxIdentifier = taxIdentifier;
        PhoneNumber = phoneNumber; DefaultCity = city; DefaultCounty = county; MarketRadiusKm = marketRadiusKm; UpdatedAt = now;
    }

    private static void ValidateRadius(int radiusKm)
    {
        if (radiusKm is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(radiusKm), "Raza de interes trebuie să fie între 1 și 1.000 km.");
    }
}
