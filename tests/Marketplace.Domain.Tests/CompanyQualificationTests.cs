using Marketplace.Domain.Profiles;
using Marketplace.Domain.Qualification;

namespace Marketplace.Domain.Tests;

public sealed class CompanyQualificationTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 31, 12, 0, 0, TimeSpan.Zero);

    private static SupplierProfile Company() => SupplierProfile.Create(
        Guid.NewGuid(), "SC Test", "J00/1/2026", "RO123", "Ion", "ion@test.local", "0700000000", null,
        new GeographicLocation("Str. 1", "Suceava", "Suceava", "România", 47.7m, 25.8m), 30, null, Now);

    [Fact]
    public void LegalProfileUpsertReplacesBankAccounts()
    {
        SupplierProfile company = Company();

        company.SetLegalProfile("Ion Popescu", "Administrator", "SRL",
            [BankAccount.Create("BCR", "RO49BCR1", "RON")], Now);

        Assert.NotNull(company.LegalProfile);
        Assert.Equal("Ion Popescu", company.LegalProfile!.LegalRepresentativeName);
        Assert.Equal("Administrator", company.LegalProfile.LegalRepresentativeRole);
        Assert.Single(company.LegalProfile.BankAccounts);
        Assert.Equal(Now, company.UpdatedAt);

        company.SetLegalProfile("Ion Popescu", "Administrator", "SRL",
            [BankAccount.Create("ING", "RO13ING2", "EUR"), BankAccount.Create("BT", "RO05BT3", "RON")], Now.AddMinutes(1));

        Assert.Equal(2, company.LegalProfile.BankAccounts.Count);
        Assert.Contains(company.LegalProfile.BankAccounts, account => account.BankName == "ING");
        Assert.DoesNotContain(company.LegalProfile.BankAccounts, account => account.BankName == "BCR");
        Assert.Equal(Now.AddMinutes(1), company.UpdatedAt);
    }

    [Fact]
    public void ExperienceCanBeAddedUpdatedAndRemoved()
    {
        SupplierProfile company = Company();

        CompanyExperience item = company.AddExperience(
            "Primăria Udești", "Reabilitare drum", "123/2024", 100000m, "RON", "45233120-6",
            new DateOnly(2024, 5, 1), null, Now);

        Assert.Single(company.Experience);
        Assert.Equal("Reabilitare drum", item.ContractTitle);
        Assert.Equal(Now, company.UpdatedAt);

        Assert.True(company.UpdateExperience(
            item.Id, "Primăria Udești", "Reabilitare drum extins", "123/2024", 120000m, "RON", "45233120-6",
            new DateOnly(2024, 6, 1), null, Now.AddMinutes(1)));
        Assert.Equal("Reabilitare drum extins", company.Experience.Single().ContractTitle);
        Assert.Equal(120000m, company.Experience.Single().ValueAmount);

        Assert.True(company.RemoveExperience(item.Id, Now.AddMinutes(2)));
        Assert.Empty(company.Experience);
        Assert.False(company.RemoveExperience(Guid.NewGuid(), Now));
    }

    [Fact]
    public void CertificateAndPersonnelFollowTheSameLifecycle()
    {
        SupplierProfile company = Company();

        CompanyCertificate certificate = company.AddCertificate(CompanyCertificateType.Iso9001, "S1", "TÜV", new DateOnly(2027, 1, 1), Now);
        Assert.Equal(CompanyCertificateType.Iso9001, certificate.CertificateType);
        Assert.True(company.UpdateCertificate(certificate.Id, CompanyCertificateType.Iso14001, "S1", "TÜV", null, Now.AddMinutes(1)));
        Assert.Equal(CompanyCertificateType.Iso14001, company.Certificates.Single().CertificateType);
        Assert.True(company.RemoveCertificate(certificate.Id, Now.AddMinutes(2)));
        Assert.Empty(company.Certificates);

        CompanyPersonnel expert = company.AddPersonnel("Maria Ionescu", "Inginer RTE", ["Autorizație RTE", "Autorizație RTE"], null, Now);
        Assert.Single(expert.ProfessionalCertificates); // normalized: distinct + trimmed
        Assert.True(company.UpdatePersonnel(expert.Id, "Maria Ionescu", "Diriginte de Șantier", ["Atestat diriginte"], "https://cv.ro", Now.AddMinutes(1)));
        Assert.Equal("Diriginte de Șantier", company.Personnel.Single().Role);
        Assert.Equal("https://cv.ro", company.Personnel.Single().CvUrl);
        Assert.True(company.RemovePersonnel(expert.Id, Now.AddMinutes(2)));
        Assert.Empty(company.Personnel);
    }

    [Fact]
    public void LegalProfileRequiresRepresentativeName()
    {
        Assert.Throws<ArgumentException>(() =>
            CompanyLegalProfile.Create(Guid.NewGuid(), "   ", null, null, [], Now));
    }

    [Fact]
    public void ExperienceRejectsNegativeValue()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CompanyExperience.Create(Guid.NewGuid(), "Client", "Contract", null, -1m, "RON", null, null, null));
    }
}
