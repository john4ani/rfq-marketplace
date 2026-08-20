namespace Marketplace.Domain.Profiles;

public enum SupplierApprovalStatus { Pending, Approved, Suspended }

public sealed class SupplierProfile
{
    private SupplierProfile() { }
    public Guid Id { get; private set; }
    public string TradingName { get; private set; } = string.Empty;
    public string CompanyRegistrationNumber { get; private set; } = string.Empty;
    public string? TaxIdentifier { get; private set; }
    public string ContactPerson { get; private set; } = string.Empty;
    public string ContactEmail { get; private set; } = string.Empty;
    public string PhoneNumber { get; private set; } = string.Empty;
    public string? Website { get; private set; }
    public string Address { get; private set; } = string.Empty;
    public string City { get; private set; } = string.Empty;
    public string County { get; private set; } = string.Empty;
    public string Country { get; private set; } = "România";
    public decimal? Latitude { get; private set; }
    public decimal? Longitude { get; private set; }
    public int MarketRadiusKm { get; private set; } = 30;
    public string? Description { get; private set; }
    public SupplierApprovalStatus ApprovalStatus { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public GeographicLocation Location => new(Address, City, County, Country, Latitude, Longitude);

    public static SupplierProfile Create(Guid id, string name, string registrationNumber, string? taxIdentifier, string contactPerson, string email, string phone, string? website, GeographicLocation location, int marketRadiusKm, string? description, DateTimeOffset now)
    {
        ValidateRadius(marketRadiusKm);
        return new() { Id = id, TradingName = name, CompanyRegistrationNumber = registrationNumber, TaxIdentifier = taxIdentifier, ContactPerson = contactPerson, ContactEmail = email, PhoneNumber = phone, Website = website, Address = location.Address ?? string.Empty, City = location.Locality, County = location.County, Country = location.Country, Latitude = location.Latitude, Longitude = location.Longitude, MarketRadiusKm = marketRadiusKm, Description = description, ApprovalStatus = SupplierApprovalStatus.Pending, IsActive = true, CreatedAt = now, UpdatedAt = now };
    }

    public void Update(string name, string registrationNumber, string? taxIdentifier, string contactPerson, string email, string phone, string? website, GeographicLocation location, int marketRadiusKm, string? description, DateTimeOffset now)
    {
        ValidateRadius(marketRadiusKm);
        TradingName = name; CompanyRegistrationNumber = registrationNumber; TaxIdentifier = taxIdentifier; ContactPerson = contactPerson;
        ContactEmail = email; PhoneNumber = phone; Website = website; Address = location.Address ?? string.Empty; City = location.Locality; County = location.County; Country = location.Country;
        Latitude = location.Latitude; Longitude = location.Longitude; MarketRadiusKm = marketRadiusKm; Description = description; UpdatedAt = now;
    }

    private static void ValidateRadius(int radiusKm)
    {
        if (radiusKm is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(radiusKm));
    }
}
