namespace Marketplace.Domain.Qualification;

/// <summary>
/// Legal/administrative qualification data of a company (1-to-0..1 with <c>SupplierProfile</c>).
/// CUI, RegCom and the registered address stay on <c>SupplierProfile</c> — this record adds only
/// what the SEAP qualification needs on top of them: the legal representative, the legal form and
/// the IBAN accounts.
/// </summary>
public sealed class CompanyLegalProfile
{
    private readonly List<BankAccount> _bankAccounts = [];
    private CompanyLegalProfile() { }

    public Guid SupplierId { get; private set; }
    public string LegalRepresentativeName { get; private set; } = string.Empty;
    public string? LegalRepresentativeRole { get; private set; }
    public string? LegalForm { get; private set; }
    public IReadOnlyCollection<BankAccount> BankAccounts => _bankAccounts;
    public DateTimeOffset UpdatedAt { get; private set; }

    public static CompanyLegalProfile Create(
        Guid supplierId, string representativeName, string? representativeRole, string? legalForm,
        IReadOnlyCollection<BankAccount> bankAccounts, DateTimeOffset now)
    {
        CompanyLegalProfile profile = new()
        {
            SupplierId = supplierId,
            LegalRepresentativeName = Required(representativeName, "Numele reprezentantului legal este obligatoriu."),
            LegalRepresentativeRole = Normalize(representativeRole),
            LegalForm = Normalize(legalForm),
            UpdatedAt = now
        };
        profile.ReplaceBankAccounts(bankAccounts);
        return profile;
    }

    public void Update(
        string representativeName, string? representativeRole, string? legalForm,
        IReadOnlyCollection<BankAccount> bankAccounts, DateTimeOffset now)
    {
        LegalRepresentativeName = Required(representativeName, "Numele reprezentantului legal este obligatoriu.");
        LegalRepresentativeRole = Normalize(representativeRole);
        LegalForm = Normalize(legalForm);
        ReplaceBankAccounts(bankAccounts);
        UpdatedAt = now;
    }

    private void ReplaceBankAccounts(IReadOnlyCollection<BankAccount> bankAccounts)
    {
        _bankAccounts.Clear();
        if (bankAccounts is null) return;
        foreach (BankAccount account in bankAccounts)
        {
            _bankAccounts.Add(account);
        }
    }

    private static string Required(string value, string message) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException(message) : value.Trim();

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
