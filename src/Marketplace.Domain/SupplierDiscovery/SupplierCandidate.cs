namespace Marketplace.Domain.SupplierDiscovery;

public enum SupplierCandidateStatus
{
    Unverified,
    EligibleForInvitation,
    Rejected,
    Claimed
}

public sealed class SupplierCandidate
{
    private readonly List<SupplierCandidateCategory> _categories = [];
    private SupplierCandidate() { }

    public Guid Id { get; private set; }
    public string CompanyName { get; private set; } = string.Empty;
    public string NormalizedCompanyName { get; private set; } = string.Empty;
    public string? ContactEmail { get; private set; }
    public string? PhoneNumber { get; private set; }
    public string? Website { get; private set; }
    public string City { get; private set; } = string.Empty;
    public string County { get; private set; } = string.Empty;
    public IReadOnlyCollection<SupplierCandidateCategory> Categories => _categories;
    public IReadOnlyCollection<Guid> CategoryIds => _categories.Select(category => category.CategoryId).ToArray();
    public string Source { get; private set; } = string.Empty;
    public string? Notes { get; private set; }
    public SupplierCandidateStatus Status { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? ReviewedBy { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }
    public string? ReviewNotes { get; private set; }
    public Guid? ClaimedBySupplierId { get; private set; }
    public DateTimeOffset? ClaimedAt { get; private set; }
    public uint Version { get; private set; }

    public static SupplierCandidate Create(
        Guid id,
        string companyName,
        string? contactEmail,
        string? phoneNumber,
        string? website,
        string city,
        string county,
        IReadOnlyCollection<Guid> categoryIds,
        string source,
        string? notes,
        Guid createdBy,
        DateTimeOffset now)
    {
        string cleanName = Required(companyName, "Denumirea companiei este obligatorie.");
        Guid[] selectedCategories = categoryIds.Where(value => value != Guid.Empty).Distinct().ToArray();
        if (selectedCategories.Length == 0)
            throw new SupplierDiscoveryRuleException("Selectează cel puțin o categorie pentru firmă.");
        var candidate = new SupplierCandidate
        {
            Id = id,
            CompanyName = cleanName,
            NormalizedCompanyName = NormalizeName(cleanName),
            ContactEmail = Clean(contactEmail),
            PhoneNumber = Clean(phoneNumber),
            Website = Clean(website),
            City = Required(city, "Localitatea este obligatorie."),
            County = Required(county, "Județul este obligatoriu."),
            Source = Required(source, "Sursa informațiilor este obligatorie."),
            Notes = Clean(notes),
            Status = SupplierCandidateStatus.Unverified,
            CreatedBy = createdBy,
            CreatedAt = now
        };
        candidate._categories.AddRange(selectedCategories.Select(categoryId =>
            SupplierCandidateCategory.Create(id, categoryId)));
        return candidate;
    }

    public void ValidateRelevance(Guid administratorId, string reviewNotes, DateTimeOffset now)
    {
        if (Status != SupplierCandidateStatus.Unverified)
            throw new SupplierDiscoveryRuleException("Doar o firmă neverificată poate fi validată pentru invitație.");
        string cleanNotes = Required(reviewNotes, "Notează pe scurt de ce firma este relevantă.");
        Status = SupplierCandidateStatus.EligibleForInvitation;
        ReviewedBy = administratorId;
        ReviewedAt = now;
        ReviewNotes = cleanNotes;
    }

    public void Reject(Guid administratorId, string reviewNotes, DateTimeOffset now)
    {
        if (Status is SupplierCandidateStatus.Claimed or SupplierCandidateStatus.Rejected)
            throw new SupplierDiscoveryRuleException("Firma nu mai poate fi respinsă în starea curentă.");
        string cleanNotes = Required(reviewNotes, "Motivul respingerii este obligatoriu.");
        Status = SupplierCandidateStatus.Rejected;
        ReviewedBy = administratorId;
        ReviewedAt = now;
        ReviewNotes = cleanNotes;
    }

    public void Claim(Guid supplierId, DateTimeOffset now)
    {
        if (Status != SupplierCandidateStatus.EligibleForInvitation)
            throw new SupplierDiscoveryRuleException("Firma nu este disponibilă pentru revendicare.");
        Status = SupplierCandidateStatus.Claimed;
        ClaimedBySupplierId = supplierId;
        ClaimedAt = now;
    }

    public void UpdateDetails(
        string companyName,
        string? contactEmail,
        string? phoneNumber,
        string? website,
        string city,
        string county,
        IReadOnlyCollection<Guid> categoryIds)
    {
        if (Status == SupplierCandidateStatus.Claimed)
            throw new SupplierDiscoveryRuleException("Firma revendicată nu mai poate fi modificată din registrul de firme descoperite.");

        string cleanName = Required(companyName, "Denumirea companiei este obligatorie.");
        Guid[] selectedCategories = categoryIds.Where(value => value != Guid.Empty).Distinct().ToArray();
        if (selectedCategories.Length == 0)
            throw new SupplierDiscoveryRuleException("Selectează cel puțin o categorie pentru firmă.");

        CompanyName = cleanName;
        NormalizedCompanyName = NormalizeName(cleanName);
        ContactEmail = Clean(contactEmail);
        PhoneNumber = Clean(phoneNumber);
        Website = Clean(website);
        City = Required(city, "Localitatea este obligatorie.");
        County = Required(county, "Județul este obligatoriu.");
        _categories.Clear();
        _categories.AddRange(selectedCategories.Select(categoryId => SupplierCandidateCategory.Create(Id, categoryId)));
    }

    private static string NormalizeName(string value) =>
        string.Join(' ', value.Trim().ToUpperInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    private static string Required(string? value, string message) =>
        string.IsNullOrWhiteSpace(value) ? throw new SupplierDiscoveryRuleException(message) : value.Trim();
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class SupplierDiscoveryRuleException(string message) : InvalidOperationException(message);
