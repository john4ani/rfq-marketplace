using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Marketplace.IntegrationTests;

[Collection(IntegrationTestGroup.Name)]
public sealed class CompanyQualificationEndpointTests : IntegrationTestBase, IClassFixture<MarketplaceWebApplicationFactory>
{
    private static readonly string[] PersonnelCerts = ["Autorizație RTE"];
    private readonly MarketplaceWebApplicationFactory factory;
    public CompanyQualificationEndpointTests(MarketplaceWebApplicationFactory factory) => this.factory = factory;

    [Fact]
    public async Task QualificationVaultSupportsTheFullCrudFlow()
    {
        using HttpClient client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        string email = $"qual-{Guid.NewGuid():N}@test.local";
        const string password = "Test123456!";
        Assert.Equal(HttpStatusCode.Created,
            (await client.PostAsJsonAsync("/api/v1/auth/register/supplier", new { email, password })).StatusCode);

        TokenResponse token = (await (await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password }))
            .Content.ReadFromJsonAsync<TokenResponse>())!;
        Guid userId = UserId(token.AccessToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        // Create the company (SupplierProfile + Owner CompanyMembership) through the real flow.
        Assert.Equal(HttpStatusCode.OK,
            (await client.PutAsJsonAsync($"/api/v1/suppliers/{userId}", SupplierProfileRequest(email))).StatusCode);

        // An untouched company has an empty vault (no legal record, no lists) — existing B2B users unaffected.
        HttpResponseMessage empty = await client.GetAsync($"/api/v1/companies/{userId}/qualification");
        Assert.Equal(HttpStatusCode.OK, empty.StatusCode);
        CompanyQualificationDto emptyVault = (await empty.Content.ReadFromJsonAsync<CompanyQualificationDto>())!;
        Assert.Null(emptyVault.Legal);
        Assert.Empty(emptyVault.Experience);
        Assert.Empty(emptyVault.Certificates);
        Assert.Empty(emptyVault.Personnel);

        // PUT legal profile with one IBAN account.
        HttpResponseMessage legal = await client.PutAsJsonAsync($"/api/v1/companies/{userId}/qualification/legal", new
        {
            legalRepresentativeName = "Ion Popescu",
            legalRepresentativeRole = "Administrator",
            legalForm = "SRL",
            bankAccounts = new[] { new { bankName = "BCR", iban = "RO49BCR12345678901234", currency = "RON" } }
        });
        Assert.Equal(HttpStatusCode.OK, legal.StatusCode);

        // Add one item to each list.
        HttpResponseMessage experience = await client.PostAsJsonAsync($"/api/v1/companies/{userId}/qualification/experience", new
        {
            clientName = "Primăria Udești", contractTitle = "Reabilitare drum", contractNumberDate = "123/2024",
            valueAmount = 100000m, valueCurrency = "RON", cpvCode = "45233120-6",
            completionDate = "2024-05-01", documentRefUrl = (string?)null
        });
        Assert.Equal(HttpStatusCode.OK, experience.StatusCode);
        CompanyExperienceDto experienceItem = (await experience.Content.ReadFromJsonAsync<CompanyExperienceDto>())!;

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/v1/companies/{userId}/qualification/certificates", new
        {
            certificateType = "Iso9001", serialNumber = "S1", issuer = "TÜV", validUntil = "2027-01-01"
        })).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/v1/companies/{userId}/qualification/personnel", new
        {
            fullName = "Maria Ionescu", role = "Inginer RTE",
            professionalCertificates = PersonnelCerts, cvUrl = (string?)null
        })).StatusCode);

        // GET returns the whole vault.
        HttpResponseMessage vault = await client.GetAsync($"/api/v1/companies/{userId}/qualification");
        Assert.Equal(HttpStatusCode.OK, vault.StatusCode);
        CompanyQualificationDto dto = (await vault.Content.ReadFromJsonAsync<CompanyQualificationDto>())!;
        Assert.Equal("Ion Popescu", dto.Legal!.LegalRepresentativeName);
        Assert.Single(dto.Legal.BankAccounts);
        Assert.Equal("RO49BCR12345678901234", dto.Legal.BankAccounts.Single().Iban);
        Assert.Single(dto.Experience);
        Assert.Single(dto.Certificates);
        Assert.Equal("Iso9001", dto.Certificates.Single().CertificateType);
        Assert.Single(dto.Personnel);
        Assert.Equal("Maria Ionescu", dto.Personnel.Single().FullName);

        // Update and remove an experience item.
        HttpResponseMessage updated = await client.PutAsJsonAsync($"/api/v1/companies/{userId}/qualification/experience/{experienceItem.Id}", new
        {
            clientName = "Primăria Udești", contractTitle = "Reabilitare drum extins", contractNumberDate = "123/2024",
            valueAmount = 120000m, valueCurrency = "RON", cpvCode = "45233120-6",
            completionDate = "2024-06-01", documentRefUrl = (string?)null
        });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal("Reabilitare drum extins", (await updated.Content.ReadFromJsonAsync<CompanyExperienceDto>())!.ContractTitle);

        Assert.Equal(HttpStatusCode.NoContent,
            (await client.DeleteAsync($"/api/v1/companies/{userId}/qualification/experience/{experienceItem.Id}")).StatusCode);

        // Another user cannot see or touch the vault (404, no existence leak).
        string otherEmail = $"qual-other-{Guid.NewGuid():N}@test.local";
        Assert.Equal(HttpStatusCode.Created,
            (await client.PostAsJsonAsync("/api/v1/auth/register/supplier", new { email = otherEmail, password })).StatusCode);
        TokenResponse other = (await (await client.PostAsJsonAsync("/api/v1/auth/login", new { email = otherEmail, password }))
            .Content.ReadFromJsonAsync<TokenResponse>())!;
        using HttpClient otherClient = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        otherClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", other.AccessToken);

        Assert.Equal(HttpStatusCode.NotFound, (await otherClient.GetAsync($"/api/v1/companies/{userId}/qualification")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await otherClient.PutAsJsonAsync($"/api/v1/companies/{userId}/qualification/legal", new
        {
            legalRepresentativeName = "Atacator", legalRepresentativeRole = (string?)null,
            legalForm = (string?)null, bankAccounts = Array.Empty<object>()
        })).StatusCode);
    }

    private static object SupplierProfileRequest(string email) => new
    {
        tradingName = "Companie calificare test", companyRegistrationNumber = $"J00/{Guid.NewGuid():N}", taxIdentifier = "RO123",
        contactPerson = "Owner test", contactEmail = email, phoneNumber = "0700000000", website = (string?)null,
        address = "Strada Test 1", city = "Iași", county = "Iași", latitude = (decimal?)null,
        longitude = (decimal?)null, description = "Profil pentru testul de calificare SEAP"
    };

    private static Guid UserId(string accessToken)
    {
        string payloadPart = accessToken.Split('.')[1];
        string payload = Encoding.UTF8.GetString(Convert.FromBase64String(
            payloadPart.PadRight(payloadPart.Length + (4 - payloadPart.Length % 4) % 4, '=').Replace('-', '+').Replace('_', '/')));
        return Guid.Parse(JsonDocument.Parse(payload).RootElement.GetProperty("sub").GetString()!);
    }

    private sealed record TokenResponse(string AccessToken, DateTimeOffset ExpiresAt);
    private sealed record CompanyQualificationDto(
        CompanyLegalProfileDto? Legal,
        IReadOnlyCollection<CompanyExperienceDto> Experience,
        IReadOnlyCollection<CompanyCertificateDto> Certificates,
        IReadOnlyCollection<CompanyPersonnelDto> Personnel);
    private sealed record CompanyLegalProfileDto(
        string LegalRepresentativeName, string? LegalRepresentativeRole, string? LegalForm,
        IReadOnlyCollection<BankAccountDto> BankAccounts);
    private sealed record BankAccountDto(string BankName, string Iban, string Currency);
    private sealed record CompanyExperienceDto(
        Guid Id, string ClientName, string ContractTitle, string? ContractNumberDate,
        decimal? ValueAmount, string? ValueCurrency, string? CpvCode, DateOnly? CompletionDate, string? DocumentRefUrl);
    private sealed record CompanyCertificateDto(Guid Id, string CertificateType, string? SerialNumber, string? Issuer, DateOnly? ValidUntil);
    private sealed record CompanyPersonnelDto(Guid Id, string FullName, string Role, IReadOnlyCollection<string> ProfessionalCertificates, string? CvUrl);
}
