namespace Marketplace.Domain.Qualification;

/// <summary>One IBAN bank account of the company (owned value object of <see cref="CompanyLegalProfile"/>).</summary>
public sealed class BankAccount
{
    private BankAccount() { }

    public string BankName { get; private set; } = string.Empty;
    public string Iban { get; private set; } = string.Empty;
    public string Currency { get; private set; } = string.Empty;

    public static BankAccount Create(string bankName, string iban, string currency) => new()
    {
        BankName = Required(bankName, "Denumirea băncii este obligatorie."),
        Iban = Required(iban, "IBAN-ul este obligatoriu."),
        Currency = Required(currency, "Valuta contului este obligatorie.")
    };

    private static string Required(string value, string message) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException(message) : value.Trim();
}
