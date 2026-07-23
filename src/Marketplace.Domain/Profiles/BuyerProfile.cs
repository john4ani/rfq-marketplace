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
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static BuyerProfile Create(Guid id, string displayName, BuyerType buyerType, string? companyName, string? taxIdentifier, string phoneNumber, string city, string county, DateTimeOffset now) =>
        new() { Id = id, DisplayName = displayName, BuyerType = buyerType, CompanyName = companyName, TaxIdentifier = taxIdentifier, PhoneNumber = phoneNumber, DefaultCity = city, DefaultCounty = county, CreatedAt = now, UpdatedAt = now };

    public void Update(string displayName, BuyerType buyerType, string? companyName, string? taxIdentifier, string phoneNumber, string city, string county, DateTimeOffset now)
    {
        DisplayName = displayName; BuyerType = buyerType; CompanyName = companyName; TaxIdentifier = taxIdentifier;
        PhoneNumber = phoneNumber; DefaultCity = city; DefaultCounty = county; UpdatedAt = now;
    }
}
