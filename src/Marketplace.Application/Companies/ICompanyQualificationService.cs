using Marketplace.Domain.Qualification;

namespace Marketplace.Application.Companies;

public sealed record BankAccountDto(string BankName, string Iban, string Currency);

public sealed record CompanyLegalProfileDto(
    string LegalRepresentativeName,
    string? LegalRepresentativeRole,
    string? LegalForm,
    IReadOnlyCollection<BankAccountDto> BankAccounts);

public sealed record CompanyExperienceDto(
    Guid Id,
    string ClientName,
    string ContractTitle,
    string? ContractNumberDate,
    decimal? ValueAmount,
    string? ValueCurrency,
    string? CpvCode,
    DateOnly? CompletionDate,
    string? DocumentRefUrl);

public sealed record CompanyCertificateDto(
    Guid Id,
    CompanyCertificateType CertificateType,
    string? SerialNumber,
    string? Issuer,
    DateOnly? ValidUntil);

public sealed record CompanyPersonnelDto(
    Guid Id,
    string FullName,
    string Role,
    IReadOnlyCollection<string> ProfessionalCertificates,
    string? CvUrl);

/// <summary>Read projection of the company's SEAP qualification vault. The legal CUI/RegCom/address
/// come from <c>SupplierProfile</c>; this DTO aggregates the qualification-specific data.</summary>
public sealed record CompanyQualificationDto(
    CompanyLegalProfileDto? Legal,
    IReadOnlyCollection<CompanyExperienceDto> Experience,
    IReadOnlyCollection<CompanyCertificateDto> Certificates,
    IReadOnlyCollection<CompanyPersonnelDto> Personnel);

// ---- Write inputs ----

public sealed record CompanyLegalProfileInput(
    string LegalRepresentativeName,
    string? LegalRepresentativeRole,
    string? LegalForm,
    IReadOnlyCollection<BankAccountDto> BankAccounts);

public sealed record CompanyExperienceInput(
    string ClientName,
    string ContractTitle,
    string? ContractNumberDate,
    decimal? ValueAmount,
    string? ValueCurrency,
    string? CpvCode,
    DateOnly? CompletionDate,
    string? DocumentRefUrl);

public sealed record CompanyCertificateInput(
    CompanyCertificateType CertificateType,
    string? SerialNumber,
    string? Issuer,
    DateOnly? ValidUntil);

public sealed record CompanyPersonnelInput(
    string FullName,
    string Role,
    IReadOnlyCollection<string> ProfessionalCertificates,
    string? CvUrl);

public interface ICompanyQualificationService
{
    Task<CompanyQualificationDto?> GetQualificationAsync(Guid companyId, CancellationToken cancellationToken);
    Task<CompanyLegalProfileDto?> UpdateLegalProfileAsync(Guid companyId, CompanyLegalProfileInput input, CancellationToken cancellationToken);
    Task<CompanyExperienceDto?> AddExperienceAsync(Guid companyId, CompanyExperienceInput input, CancellationToken cancellationToken);
    Task<CompanyExperienceDto?> UpdateExperienceAsync(Guid companyId, Guid experienceId, CompanyExperienceInput input, CancellationToken cancellationToken);
    Task<bool> RemoveExperienceAsync(Guid companyId, Guid experienceId, CancellationToken cancellationToken);
    Task<CompanyCertificateDto?> AddCertificateAsync(Guid companyId, CompanyCertificateInput input, CancellationToken cancellationToken);
    Task<CompanyCertificateDto?> UpdateCertificateAsync(Guid companyId, Guid certificateId, CompanyCertificateInput input, CancellationToken cancellationToken);
    Task<bool> RemoveCertificateAsync(Guid companyId, Guid certificateId, CancellationToken cancellationToken);
    Task<CompanyPersonnelDto?> AddPersonnelAsync(Guid companyId, CompanyPersonnelInput input, CancellationToken cancellationToken);
    Task<CompanyPersonnelDto?> UpdatePersonnelAsync(Guid companyId, Guid personnelId, CompanyPersonnelInput input, CancellationToken cancellationToken);
    Task<bool> RemovePersonnelAsync(Guid companyId, Guid personnelId, CancellationToken cancellationToken);
}
