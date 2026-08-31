using Marketplace.Application.Companies;
using Marketplace.Domain.Profiles;
using Marketplace.Domain.Qualification;
using Marketplace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Infrastructure.Companies;

/// <summary>
/// Company SEAP qualification vault: legal/administrative data, similar-experience portfolio,
/// certificates and key experts. All collections are optional and empty by default, so existing
/// B2B companies are unaffected. The legal CUI/RegCom/address remain on <c>SupplierProfile</c>.
/// </summary>
public sealed class CompanyQualificationService(MarketplaceDbContext dbContext, TimeProvider timeProvider) : ICompanyQualificationService
{
    public async Task<CompanyQualificationDto?> GetQualificationAsync(Guid companyId, CancellationToken cancellationToken)
    {
        SupplierProfile? profile = await LoadProfileAsync(companyId, cancellationToken);
        return profile is null ? null : ToDto(profile);
    }

    public async Task<CompanyLegalProfileDto?> UpdateLegalProfileAsync(Guid companyId, CompanyLegalProfileInput input, CancellationToken cancellationToken)
    {
        SupplierProfile? profile = await LoadProfileAsync(companyId, cancellationToken);
        if (profile is null) return null;
        BankAccount[] accounts = (input.BankAccounts ?? [])
            .Select(account => BankAccount.Create(account.BankName, account.Iban, account.Currency))
            .ToArray();
        profile.SetLegalProfile(input.LegalRepresentativeName, input.LegalRepresentativeRole, input.LegalForm, accounts, timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToLegalDto(profile.LegalProfile!);
    }

    public async Task<CompanyExperienceDto?> AddExperienceAsync(Guid companyId, CompanyExperienceInput input, CancellationToken cancellationToken)
    {
        SupplierProfile? profile = await LoadProfileAsync(companyId, cancellationToken);
        if (profile is null) return null;
        CompanyExperience item = profile.AddExperience(
            input.ClientName, input.ContractTitle, input.ContractNumberDate, input.ValueAmount,
            input.ValueCurrency, input.CpvCode, input.CompletionDate, input.DocumentRefUrl, timeProvider.GetUtcNow());
        // Same guard as CompanyVaultService/AutoBiddingService: EF Core 10's DetectChanges can classify a
        // new entity added to an already-tracked collection as Modified, turning the save into a no-op UPDATE.
        if (dbContext.Entry(item).State != EntityState.Added) dbContext.CompanyExperiences.Add(item);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToExperienceDto(item);
    }

    public async Task<CompanyExperienceDto?> UpdateExperienceAsync(Guid companyId, Guid experienceId, CompanyExperienceInput input, CancellationToken cancellationToken)
    {
        SupplierProfile? profile = await LoadProfileAsync(companyId, cancellationToken);
        if (profile is null) return null;
        bool updated = profile.UpdateExperience(
            experienceId, input.ClientName, input.ContractTitle, input.ContractNumberDate, input.ValueAmount,
            input.ValueCurrency, input.CpvCode, input.CompletionDate, input.DocumentRefUrl, timeProvider.GetUtcNow());
        if (!updated) return null;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToExperienceDto(profile.Experience.Single(item => item.Id == experienceId));
    }

    public async Task<bool> RemoveExperienceAsync(Guid companyId, Guid experienceId, CancellationToken cancellationToken)
    {
        SupplierProfile? profile = await LoadProfileAsync(companyId, cancellationToken);
        if (profile is null) return false;
        if (!profile.RemoveExperience(experienceId, timeProvider.GetUtcNow())) return false;
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<CompanyCertificateDto?> AddCertificateAsync(Guid companyId, CompanyCertificateInput input, CancellationToken cancellationToken)
    {
        SupplierProfile? profile = await LoadProfileAsync(companyId, cancellationToken);
        if (profile is null) return null;
        CompanyCertificate item = profile.AddCertificate(input.CertificateType, input.SerialNumber, input.Issuer, input.ValidUntil, timeProvider.GetUtcNow());
        // Same guard as CompanyVaultService/AutoBiddingService: EF Core 10's DetectChanges can classify a
        // new entity added to an already-tracked collection as Modified, turning the save into a no-op UPDATE.
        if (dbContext.Entry(item).State != EntityState.Added) dbContext.CompanyCertificates.Add(item);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToCertificateDto(item);
    }

    public async Task<CompanyCertificateDto?> UpdateCertificateAsync(Guid companyId, Guid certificateId, CompanyCertificateInput input, CancellationToken cancellationToken)
    {
        SupplierProfile? profile = await LoadProfileAsync(companyId, cancellationToken);
        if (profile is null) return null;
        bool updated = profile.UpdateCertificate(certificateId, input.CertificateType, input.SerialNumber, input.Issuer, input.ValidUntil, timeProvider.GetUtcNow());
        if (!updated) return null;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToCertificateDto(profile.Certificates.Single(item => item.Id == certificateId));
    }

    public async Task<bool> RemoveCertificateAsync(Guid companyId, Guid certificateId, CancellationToken cancellationToken)
    {
        SupplierProfile? profile = await LoadProfileAsync(companyId, cancellationToken);
        if (profile is null) return false;
        if (!profile.RemoveCertificate(certificateId, timeProvider.GetUtcNow())) return false;
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<CompanyPersonnelDto?> AddPersonnelAsync(Guid companyId, CompanyPersonnelInput input, CancellationToken cancellationToken)
    {
        SupplierProfile? profile = await LoadProfileAsync(companyId, cancellationToken);
        if (profile is null) return null;
        CompanyPersonnel item = profile.AddPersonnel(input.FullName, input.Role, input.ProfessionalCertificates, input.CvUrl, timeProvider.GetUtcNow());
        // Same guard as CompanyVaultService/AutoBiddingService: EF Core 10's DetectChanges can classify a
        // new entity added to an already-tracked collection as Modified, turning the save into a no-op UPDATE.
        if (dbContext.Entry(item).State != EntityState.Added) dbContext.CompanyPersonnel.Add(item);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToPersonnelDto(item);
    }

    public async Task<CompanyPersonnelDto?> UpdatePersonnelAsync(Guid companyId, Guid personnelId, CompanyPersonnelInput input, CancellationToken cancellationToken)
    {
        SupplierProfile? profile = await LoadProfileAsync(companyId, cancellationToken);
        if (profile is null) return null;
        bool updated = profile.UpdatePersonnel(personnelId, input.FullName, input.Role, input.ProfessionalCertificates, input.CvUrl, timeProvider.GetUtcNow());
        if (!updated) return null;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToPersonnelDto(profile.Personnel.Single(item => item.Id == personnelId));
    }

    public async Task<bool> RemovePersonnelAsync(Guid companyId, Guid personnelId, CancellationToken cancellationToken)
    {
        SupplierProfile? profile = await LoadProfileAsync(companyId, cancellationToken);
        if (profile is null) return false;
        if (!profile.RemovePersonnel(personnelId, timeProvider.GetUtcNow())) return false;
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private Task<SupplierProfile?> LoadProfileAsync(Guid companyId, CancellationToken cancellationToken) =>
        dbContext.SupplierProfiles
            .Include(profile => profile.LegalProfile)
            .Include(profile => profile.Experience)
            .Include(profile => profile.Certificates)
            .Include(profile => profile.Personnel)
            .SingleOrDefaultAsync(profile => profile.Id == companyId, cancellationToken);

    private static CompanyQualificationDto ToDto(SupplierProfile profile) => new(
        profile.LegalProfile is null ? null : ToLegalDto(profile.LegalProfile),
        profile.Experience.Select(ToExperienceDto).ToArray(),
        profile.Certificates.Select(ToCertificateDto).ToArray(),
        profile.Personnel.Select(ToPersonnelDto).ToArray());

    private static CompanyLegalProfileDto ToLegalDto(CompanyLegalProfile profile) => new(
        profile.LegalRepresentativeName,
        profile.LegalRepresentativeRole,
        profile.LegalForm,
        profile.BankAccounts.Select(account => new BankAccountDto(account.BankName, account.Iban, account.Currency)).ToArray());

    private static CompanyExperienceDto ToExperienceDto(CompanyExperience item) => new(
        item.Id, item.ClientName, item.ContractTitle, item.ContractNumberDate, item.ValueAmount,
        item.ValueCurrency, item.CpvCode, item.CompletionDate, item.DocumentRefUrl);

    private static CompanyCertificateDto ToCertificateDto(CompanyCertificate item) => new(
        item.Id, item.CertificateType, item.SerialNumber, item.Issuer, item.ValidUntil);

    private static CompanyPersonnelDto ToPersonnelDto(CompanyPersonnel item) => new(
        item.Id, item.FullName, item.Role, item.ProfessionalCertificates, item.CvUrl);
}
