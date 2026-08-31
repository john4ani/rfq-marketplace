namespace Marketplace.Domain.Profiles;

using Marketplace.Domain.Qualification;

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

    /// <summary>Legal/administrative qualification data (1-to-0..1); null until the company saves it.</summary>
    public CompanyLegalProfile? LegalProfile { get; private set; }
    private readonly List<CompanyExperience> _experience = [];
    private readonly List<CompanyCertificate> _certificates = [];
    private readonly List<CompanyPersonnel> _personnel = [];
    /// <summary>Previous contracts proving similar experience; empty until added.</summary>
    public IReadOnlyCollection<CompanyExperience> Experience => _experience;
    /// <summary>Certificates / authorizations (ISO, ANRE, CNSAS, …); empty until added.</summary>
    public IReadOnlyCollection<CompanyCertificate> Certificates => _certificates;
    /// <summary>Key experts (Inginer RTE, Diriginte de Șantier, Arhitect, …); empty until added.</summary>
    public IReadOnlyCollection<CompanyPersonnel> Personnel => _personnel;

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

    /// <summary>Upserts the company's legal/administrative qualification record (replaces the IBAN list).</summary>
    public CompanyLegalProfile SetLegalProfile(
        string representativeName, string? representativeRole, string? legalForm,
        IReadOnlyCollection<BankAccount> bankAccounts, DateTimeOffset now)
    {
        if (LegalProfile is null)
            LegalProfile = CompanyLegalProfile.Create(Id, representativeName, representativeRole, legalForm, bankAccounts, now);
        else
            LegalProfile.Update(representativeName, representativeRole, legalForm, bankAccounts, now);
        UpdatedAt = now;
        return LegalProfile;
    }

    public CompanyExperience AddExperience(
        string clientName, string contractTitle, string? contractNumberDate, decimal? valueAmount,
        string? valueCurrency, string? cpvCode, DateOnly? completionDate, string? documentRefUrl, DateTimeOffset now)
    {
        CompanyExperience experience = CompanyExperience.Create(
            Id, clientName, contractTitle, contractNumberDate, valueAmount, valueCurrency, cpvCode, completionDate, documentRefUrl);
        _experience.Add(experience);
        UpdatedAt = now;
        return experience;
    }

    public bool UpdateExperience(
        Guid experienceId, string clientName, string contractTitle, string? contractNumberDate,
        decimal? valueAmount, string? valueCurrency, string? cpvCode, DateOnly? completionDate, string? documentRefUrl, DateTimeOffset now)
    {
        CompanyExperience? item = _experience.SingleOrDefault(entry => entry.Id == experienceId);
        if (item is null) return false;
        item.Update(clientName, contractTitle, contractNumberDate, valueAmount, valueCurrency, cpvCode, completionDate, documentRefUrl);
        UpdatedAt = now;
        return true;
    }

    public bool RemoveExperience(Guid experienceId, DateTimeOffset now)
    {
        bool removed = _experience.RemoveAll(entry => entry.Id == experienceId) > 0;
        if (removed) UpdatedAt = now;
        return removed;
    }

    public CompanyCertificate AddCertificate(CompanyCertificateType certificateType, string? serialNumber, string? issuer, DateOnly? validUntil, DateTimeOffset now)
    {
        CompanyCertificate certificate = CompanyCertificate.Create(Id, certificateType, serialNumber, issuer, validUntil);
        _certificates.Add(certificate);
        UpdatedAt = now;
        return certificate;
    }

    public bool UpdateCertificate(Guid certificateId, CompanyCertificateType certificateType, string? serialNumber, string? issuer, DateOnly? validUntil, DateTimeOffset now)
    {
        CompanyCertificate? item = _certificates.SingleOrDefault(entry => entry.Id == certificateId);
        if (item is null) return false;
        item.Update(certificateType, serialNumber, issuer, validUntil);
        UpdatedAt = now;
        return true;
    }

    public bool RemoveCertificate(Guid certificateId, DateTimeOffset now)
    {
        bool removed = _certificates.RemoveAll(entry => entry.Id == certificateId) > 0;
        if (removed) UpdatedAt = now;
        return removed;
    }

    public CompanyPersonnel AddPersonnel(string fullName, string role, IReadOnlyCollection<string> professionalCertificates, string? cvUrl, DateTimeOffset now)
    {
        CompanyPersonnel personnel = CompanyPersonnel.Create(Id, fullName, role, professionalCertificates, cvUrl);
        _personnel.Add(personnel);
        UpdatedAt = now;
        return personnel;
    }

    public bool UpdatePersonnel(Guid personnelId, string fullName, string role, IReadOnlyCollection<string> professionalCertificates, string? cvUrl, DateTimeOffset now)
    {
        CompanyPersonnel? item = _personnel.SingleOrDefault(entry => entry.Id == personnelId);
        if (item is null) return false;
        item.Update(fullName, role, professionalCertificates, cvUrl);
        UpdatedAt = now;
        return true;
    }

    public bool RemovePersonnel(Guid personnelId, DateTimeOffset now)
    {
        bool removed = _personnel.RemoveAll(entry => entry.Id == personnelId) > 0;
        if (removed) UpdatedAt = now;
        return removed;
    }

    private static void ValidateRadius(int radiusKm)
    {
        if (radiusKm is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(radiusKm));
    }
}
