namespace Marketplace.Domain.Qualification;

/// <summary>One key expert of the company (Inginer RTE, Diriginte de Șantier, Arhitect, …).</summary>
public sealed class CompanyPersonnel
{
    private CompanyPersonnel() { }

    public Guid Id { get; private set; }
    public Guid SupplierId { get; private set; }
    public string FullName { get; private set; } = string.Empty;
    public string Role { get; private set; } = string.Empty;
    /// <summary>Professional certificates / attestations of this expert, e.g. „Autorizație RTE grad I".</summary>
    public IReadOnlyCollection<string> ProfessionalCertificates { get; private set; } = [];
    public string? CvUrl { get; private set; }

    public static CompanyPersonnel Create(
        Guid supplierId, string fullName, string role, IReadOnlyCollection<string> professionalCertificates, string? cvUrl) => new()
    {
        Id = Guid.NewGuid(),
        SupplierId = supplierId,
        FullName = Required(fullName, "Numele expertului este obligatoriu."),
        Role = Required(role, "Rolul expertului este obligatoriu."),
        ProfessionalCertificates = NormalizeCertificates(professionalCertificates),
        CvUrl = Normalize(cvUrl)
    };

    public void Update(string fullName, string role, IReadOnlyCollection<string> professionalCertificates, string? cvUrl)
    {
        FullName = Required(fullName, "Numele expertului este obligatoriu.");
        Role = Required(role, "Rolul expertului este obligatoriu.");
        ProfessionalCertificates = NormalizeCertificates(professionalCertificates);
        CvUrl = Normalize(cvUrl);
    }

    private static string[] NormalizeCertificates(IReadOnlyCollection<string>? certificates) =>
        (certificates ?? [])
            .Where(certificate => !string.IsNullOrWhiteSpace(certificate))
            .Select(certificate => certificate.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(certificate => certificate, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static string Required(string value, string message) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException(message) : value.Trim();

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
