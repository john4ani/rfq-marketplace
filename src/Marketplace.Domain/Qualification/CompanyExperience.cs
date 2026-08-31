namespace Marketplace.Domain.Qualification;

/// <summary>One previous contract of the company — the portfolio that proves similar experience.</summary>
public sealed class CompanyExperience
{
    private CompanyExperience() { }

    public Guid Id { get; private set; }
    public Guid SupplierId { get; private set; }
    public string ClientName { get; private set; } = string.Empty;
    public string ContractTitle { get; private set; } = string.Empty;
    /// <summary>Contract number and date as stated in the record, e.g. „NR. 123 / 12.03.2024".</summary>
    public string? ContractNumberDate { get; private set; }
    public decimal? ValueAmount { get; private set; }
    public string? ValueCurrency { get; private set; }
    public string? CpvCode { get; private set; }
    public DateOnly? CompletionDate { get; private set; }
    public string? DocumentRefUrl { get; private set; }

    public static CompanyExperience Create(
        Guid supplierId, string clientName, string contractTitle, string? contractNumberDate,
        decimal? valueAmount, string? valueCurrency, string? cpvCode, DateOnly? completionDate, string? documentRefUrl) => new()
    {
        Id = Guid.NewGuid(),
        SupplierId = supplierId,
        ClientName = Required(clientName, "Numele clientului este obligatoriu."),
        ContractTitle = Required(contractTitle, "Titlul contractului este obligatoriu."),
        ContractNumberDate = Normalize(contractNumberDate),
        ValueAmount = valueAmount is null ? null : ValidateAmount(valueAmount.Value),
        ValueCurrency = Normalize(valueCurrency),
        CpvCode = Normalize(cpvCode),
        CompletionDate = completionDate,
        DocumentRefUrl = Normalize(documentRefUrl)
    };

    public void Update(
        string clientName, string contractTitle, string? contractNumberDate,
        decimal? valueAmount, string? valueCurrency, string? cpvCode, DateOnly? completionDate, string? documentRefUrl)
    {
        ClientName = Required(clientName, "Numele clientului este obligatoriu.");
        ContractTitle = Required(contractTitle, "Titlul contractului este obligatoriu.");
        ContractNumberDate = Normalize(contractNumberDate);
        ValueAmount = valueAmount is null ? null : ValidateAmount(valueAmount.Value);
        ValueCurrency = Normalize(valueCurrency);
        CpvCode = Normalize(cpvCode);
        CompletionDate = completionDate;
        DocumentRefUrl = Normalize(documentRefUrl);
    }

    private static decimal ValidateAmount(decimal value) =>
        value < 0 ? throw new ArgumentOutOfRangeException(nameof(value), "Valoarea contractului nu poate fi negativă.") : value;

    private static string Required(string value, string message) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException(message) : value.Trim();

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
