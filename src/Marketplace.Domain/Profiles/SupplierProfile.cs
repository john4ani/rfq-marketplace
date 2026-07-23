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
    public decimal? Latitude { get; private set; }
    public decimal? Longitude { get; private set; }
    public string? Description { get; private set; }
    public SupplierApprovalStatus ApprovalStatus { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static SupplierProfile Create(Guid id, string name, string registrationNumber, string? taxIdentifier, string contactPerson, string email, string phone, string? website, string address, string city, string county, decimal? latitude, decimal? longitude, string? description, DateTimeOffset now) =>
        new() { Id = id, TradingName = name, CompanyRegistrationNumber = registrationNumber, TaxIdentifier = taxIdentifier, ContactPerson = contactPerson, ContactEmail = email, PhoneNumber = phone, Website = website, Address = address, City = city, County = county, Latitude = latitude, Longitude = longitude, Description = description, ApprovalStatus = SupplierApprovalStatus.Pending, IsActive = true, CreatedAt = now, UpdatedAt = now };

    public void Update(string name, string registrationNumber, string? taxIdentifier, string contactPerson, string email, string phone, string? website, string address, string city, string county, decimal? latitude, decimal? longitude, string? description, DateTimeOffset now)
    {
        TradingName = name; CompanyRegistrationNumber = registrationNumber; TaxIdentifier = taxIdentifier; ContactPerson = contactPerson;
        ContactEmail = email; PhoneNumber = phone; Website = website; Address = address; City = city; County = county;
        Latitude = latitude; Longitude = longitude; Description = description; UpdatedAt = now;
    }
}
