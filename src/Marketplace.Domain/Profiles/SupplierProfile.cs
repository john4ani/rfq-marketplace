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
    /// <summary>CPV prefixes configured through the SEAP Tender Match profile (e.g. "45233000"). Used as
    /// deterministic prefix filters over <c>SeapTender.CpvCodes</c> in the Anunțuri SEAP list.</summary>
    public IReadOnlyCollection<string> SeapCpvPrefixes { get; private set; } = [];
    /// <summary>Canonical county names (e.g. "Iași") configured through the SEAP Tender Match profile;
    /// matched exactly against <c>SeapTender.County</c> in the Anunțuri SEAP list.</summary>
    public IReadOnlyCollection<string> SeapCounties { get; private set; } = [];
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

    /// <summary>Replaces the SEAP Tender Match filter profile (CPV prefixes + canonical county names).
    /// Values are normalized: trimmed, distinct and ordered.</summary>
    public void SetSeapFilters(IReadOnlyCollection<string> cpvPrefixes, IReadOnlyCollection<string> counties, DateTimeOffset now)
    {
        SeapCpvPrefixes = (cpvPrefixes ?? [])
            .Where(prefix => !string.IsNullOrWhiteSpace(prefix))
            .Select(prefix => prefix.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(prefix => prefix, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        SeapCounties = (counties ?? [])
            .Where(county => !string.IsNullOrWhiteSpace(county))
            .Select(county => county.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(county => county, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        UpdatedAt = now;
    }

    public void SetActive(bool active, DateTimeOffset now)
    {
        IsActive = active;
        UpdatedAt = now;
    }

    private static void ValidateRadius(int radiusKm)
    {
        if (radiusKm is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(radiusKm));
    }
}
