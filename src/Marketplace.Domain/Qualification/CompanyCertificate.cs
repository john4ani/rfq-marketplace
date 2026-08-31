namespace Marketplace.Domain.Qualification;

public enum CompanyCertificateType
{
    Iso9001,
    Iso14001,
    Iso45001,
    Anre,
    Cnsas,
    Other
}

/// <summary>One certificate / authorization of the company (ISO, ANRE, CNSAS, …).</summary>
public sealed class CompanyCertificate
{
    private CompanyCertificate() { }

    public Guid Id { get; private set; }
    public Guid SupplierId { get; private set; }
    public CompanyCertificateType CertificateType { get; private set; }
    public string? SerialNumber { get; private set; }
    public string? Issuer { get; private set; }
    public DateOnly? ValidUntil { get; private set; }

    public static CompanyCertificate Create(
        Guid supplierId, CompanyCertificateType certificateType, string? serialNumber, string? issuer, DateOnly? validUntil) => new()
    {
        Id = Guid.NewGuid(),
        SupplierId = supplierId,
        CertificateType = certificateType,
        SerialNumber = Normalize(serialNumber),
        Issuer = Normalize(issuer),
        ValidUntil = validUntil
    };

    public void Update(CompanyCertificateType certificateType, string? serialNumber, string? issuer, DateOnly? validUntil)
    {
        CertificateType = certificateType;
        SerialNumber = Normalize(serialNumber);
        Issuer = Normalize(issuer);
        ValidUntil = validUntil;
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
