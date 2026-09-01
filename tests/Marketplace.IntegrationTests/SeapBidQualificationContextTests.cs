using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Marketplace.Domain.Seap;
using Marketplace.Infrastructure.Bidding;
using Marketplace.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Marketplace.IntegrationTests;

// The SEAP auto-bid generation must inject the requesting company's qualification vault (legal data,
// portfolio, certificates, key experts) into the LLM prompt, so the generated .docx carries real company
// data. When no qualification data is available the generation never blocks — the prompt falls back to
// clearly marked „[De completat: …]\" placeholders and the document still renders.
[Collection(IntegrationTestGroup.Name)]
public sealed class SeapBidQualificationContextTests : IntegrationTestBase, IClassFixture<MarketplaceWebApplicationFactory>
{
    private static readonly string[] PersonnelCerts = ["Autorizație RTE"];
    private readonly MarketplaceWebApplicationFactory factory;
    public SeapBidQualificationContextTests(MarketplaceWebApplicationFactory factory) => this.factory = factory;

    [Fact]
    public async Task AutoBidInjectsCompanyQualificationDataIntoTheGeneratedDocument()
    {
        QualificationEchoLlm echo = new();
        using WebApplicationFactory<Program> bidFactory = NewBidFactory(echo);
        using HttpClient client = bidFactory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });

        Guid companyId = await RegisterCompanyWithQualificationAsync(client);

        Guid tenderId = await SeedTenderAsync();

        HttpResponseMessage autoBid = await client.PostAsJsonAsync($"/api/v1/seap/tenders/{tenderId}/auto-bid",
            new { companyId });
        Assert.Equal(HttpStatusCode.OK, autoBid.StatusCode);

        // The prompt must carry the qualification block with the real company data.
        string bidPrompt = AutoBidPrompt(echo);
        Assert.Contains("Date de calificare ale firmei (din Vault-ul de calificare):", bidPrompt);
        Assert.Contains("Reprezentant legal: Ion Popescu (Administrator)", bidPrompt);
        Assert.Contains("Cont IBAN: BCR RO49BCR12345678901234 (RON)", bidPrompt);
        Assert.Contains("Experiență similară:", bidPrompt);
        Assert.Contains("Primăria Udești", bidPrompt);
        Assert.Contains("Certificate:", bidPrompt);
        Assert.Contains("ISO 9001", bidPrompt);
        Assert.Contains("Experți cheie:", bidPrompt);
        Assert.Contains("Maria Ionescu — Inginer RTE", bidPrompt);

        // The generated .docx must contain the real company data (echoed by the mock LLM into sections).
        byte[] docx = await DownloadAsync(client, tenderId, "TechnicalProposal");
        string docText = ExtractDocxText(docx);
        Assert.Contains("Ion Popescu", docText);
        Assert.Contains("Maria Ionescu", docText);
        Assert.Contains("BCR RO49BCR12345678901234", docText);
    }

    [Fact]
    public async Task AutoBidFallsBackToPlaceholdersWhenTheQualificationVaultIsEmpty()
    {
        QualificationEchoLlm echo = new();
        using WebApplicationFactory<Program> bidFactory = NewBidFactory(echo);
        using HttpClient client = bidFactory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });

        // Register + company WITHOUT any qualification data.
        string email = $"seap-qual-empty-{Guid.NewGuid():N}@test.local";
        const string password = "Test123456!";
        Assert.Equal(HttpStatusCode.Created,
            (await client.PostAsJsonAsync("/api/v1/auth/register/supplier", new { email, password })).StatusCode);
        TokenResponse token = (await (await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password }))
            .Content.ReadFromJsonAsync<TokenResponse>())!;
        Guid companyId = UserId(token.AccessToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        Assert.Equal(HttpStatusCode.OK,
            (await client.PutAsJsonAsync($"/api/v1/suppliers/{companyId}", SupplierProfileRequest(email))).StatusCode);

        Guid tenderId = await SeedTenderAsync();

        // Non-gate: generation succeeds even with an empty qualification vault.
        HttpResponseMessage autoBid = await client.PostAsJsonAsync($"/api/v1/seap/tenders/{tenderId}/auto-bid",
            new { companyId });
        Assert.Equal(HttpStatusCode.OK, autoBid.StatusCode);

        string bidPrompt = AutoBidPrompt(echo);
        Assert.Contains("nu sunt disponibile în Vault-ul de calificare", bidPrompt);
        Assert.Contains("[De completat", bidPrompt);

        // The mock LLM echoes the placeholder, so the generated .docx carries it instead of invented data.
        byte[] docx = await DownloadAsync(client, tenderId, "TechnicalProposal");
        Assert.Contains("[De completat: Reprezentant Legal]", ExtractDocxText(docx));
    }

    [Fact]
    public async Task AutoBidRejectsACompanyTheCallerDoesNotRepresent()
    {
        QualificationEchoLlm echo = new();
        using WebApplicationFactory<Program> bidFactory = NewBidFactory(echo);
        using HttpClient ownerClient = bidFactory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });

        string ownerEmail = $"seap-qual-owner-{Guid.NewGuid():N}@test.local";
        const string password = "Test123456!";
        Assert.Equal(HttpStatusCode.Created,
            (await ownerClient.PostAsJsonAsync("/api/v1/auth/register/supplier", new { email = ownerEmail, password })).StatusCode);
        TokenResponse ownerToken = (await (await ownerClient.PostAsJsonAsync("/api/v1/auth/login", new { email = ownerEmail, password }))
            .Content.ReadFromJsonAsync<TokenResponse>())!;
        Guid companyId = UserId(ownerToken.AccessToken);
        ownerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken.AccessToken);
        Assert.Equal(HttpStatusCode.OK,
            (await ownerClient.PutAsJsonAsync($"/api/v1/suppliers/{companyId}", SupplierProfileRequest(ownerEmail))).StatusCode);

        // A different user that does not represent the company must not start a bid for it (404, no leak).
        string intruderEmail = $"seap-qual-intruder-{Guid.NewGuid():N}@test.local";
        Assert.Equal(HttpStatusCode.Created,
            (await ownerClient.PostAsJsonAsync("/api/v1/auth/register/supplier", new { email = intruderEmail, password })).StatusCode);
        TokenResponse intruderToken = (await (await ownerClient.PostAsJsonAsync("/api/v1/auth/login", new { email = intruderEmail, password }))
            .Content.ReadFromJsonAsync<TokenResponse>())!;
        using HttpClient intruderClient = bidFactory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        intruderClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", intruderToken.AccessToken);

        Guid tenderId = await SeedTenderAsync();
        Assert.Equal(HttpStatusCode.NotFound,
            (await intruderClient.PostAsJsonAsync($"/api/v1/seap/tenders/{tenderId}/auto-bid", new { companyId })).StatusCode);
    }

    private WebApplicationFactory<Program> NewBidFactory(QualificationEchoLlm echo) =>
        factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["Features:AutoBiddingEnabled"] = "true" }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ILlmProvider>();
                services.AddSingleton<ILlmProvider>(echo);
            });
        });

    private static async Task<Guid> RegisterCompanyWithQualificationAsync(HttpClient client)
    {
        string email = $"seap-qual-{Guid.NewGuid():N}@test.local";
        const string password = "Test123456!";
        Assert.Equal(HttpStatusCode.Created,
            (await client.PostAsJsonAsync("/api/v1/auth/register/supplier", new { email, password })).StatusCode);
        TokenResponse token = (await (await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password }))
            .Content.ReadFromJsonAsync<TokenResponse>())!;
        Guid companyId = UserId(token.AccessToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        Assert.Equal(HttpStatusCode.OK,
            (await client.PutAsJsonAsync($"/api/v1/suppliers/{companyId}", SupplierProfileRequest(email))).StatusCode);

        // Seed the qualification vault through the real API.
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/v1/companies/{companyId}/qualification/legal", new
        {
            legalRepresentativeName = "Ion Popescu",
            legalRepresentativeRole = "Administrator",
            legalForm = "SRL",
            bankAccounts = new[] { new { bankName = "BCR", iban = "RO49BCR12345678901234", currency = "RON" } }
        })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/v1/companies/{companyId}/qualification/experience", new
        {
            clientName = "Primăria Udești", contractTitle = "Reabilitare drum", contractNumberDate = "123/2024",
            valueAmount = 100000m, valueCurrency = "RON", cpvCode = "45233120-6",
            completionDate = "2024-05-01", documentRefUrl = (string?)null
        })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/v1/companies/{companyId}/qualification/certificates", new
        {
            certificateType = "Iso9001", serialNumber = "S1", issuer = "TÜV", validUntil = "2027-01-01"
        })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/v1/companies/{companyId}/qualification/personnel", new
        {
            fullName = "Maria Ionescu", role = "Inginer RTE",
            professionalCertificates = PersonnelCerts, cvUrl = (string?)null
        })).StatusCode);
        return companyId;
    }

    private async Task<Guid> SeedTenderAsync()
    {
        using IServiceScope scope = factory.Services.CreateScope();
        MarketplaceDbContext db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
        SeapTender tender = SeapTender.Import(
            $"EXT-{Guid.NewGuid():N}", "Lucrări de construcții grădiniță", "Caiet de sarcini pentru lucrări de construcții",
            ["45214100-1"], ["Lucrari de constructii de gradinite"], 2_420_466m, "RON",
            "Primaria Comunei Poiana Dambovita", "Dâmbovița",
            DateTimeOffset.UtcNow.AddDays(-2), DateTimeOffset.UtcNow.AddDays(30), DateTimeOffset.UtcNow);
        db.SeapTenders.Add(tender);
        await db.SaveChangesAsync();
        return tender.Id;
    }

    /// <summary>Selects the auto-bid LLM call deterministically: the SEAP background worker also calls the
    /// LLM (summary/categorization) on its own schedule, so the last captured prompt is not necessarily
    /// the auto-bid one. The auto-bid user prompt always starts with the requirements marker.</summary>
    private static string AutoBidPrompt(QualificationEchoLlm echo) =>
        echo.UserPrompts.Single(prompt => prompt.Contains("Cerințele anunțului:", StringComparison.Ordinal));

    private static async Task<byte[]> DownloadAsync(HttpClient client, Guid tenderId, string kind)
    {
        HttpResponseMessage response = await client.GetAsync($"/api/v1/seap/tenders/{tenderId}/bid-file?kind={kind}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsByteArrayAsync();
    }

    private static string ExtractDocxText(byte[] docx)
    {
        using MemoryStream stream = new(docx);
        using WordprocessingDocument document = WordprocessingDocument.Open(stream, false);
        return document.MainDocumentPart?.Document?.Body?
            .Descendants<Text>()
            .Select(node => node.Text)
            .Aggregate(new StringBuilder(), (builder, value) => builder.Append(value))
            .ToString() ?? string.Empty;
    }

    private static object SupplierProfileRequest(string email) => new
    {
        tradingName = "Companie calificare SEAP test", companyRegistrationNumber = $"J00/{Guid.NewGuid():N}", taxIdentifier = "RO123",
        contactPerson = "Owner test", contactEmail = email, phoneNumber = "0700000000", website = (string?)null,
        address = "Strada Test 1", city = "Iași", county = "Iași", latitude = (decimal?)null,
        longitude = (decimal?)null, description = "Profil pentru testul de context de calificare SEAP"
    };

    private static Guid UserId(string accessToken)
    {
        string payloadPart = accessToken.Split('.')[1];
        string payload = Encoding.UTF8.GetString(Convert.FromBase64String(
            payloadPart.PadRight(payloadPart.Length + (4 - payloadPart.Length % 4) % 4, '=').Replace('-', '+').Replace('_', '/')));
        return Guid.Parse(JsonDocument.Parse(payload).RootElement.GetProperty("sub").GetString()!);
    }

    private sealed record TokenResponse(string AccessToken, DateTimeOffset ExpiresAt);

    /// <summary>Records every user prompt (the SEAP background worker also calls the LLM) and returns a
    /// fixed proposal that echoes the company data found in the qualification block — so the generated
    /// .docx deterministically proves the injection.</summary>
    private sealed class QualificationEchoLlm : ILlmProvider
    {
        public List<string> UserPrompts { get; } = [];

        public Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken)
        {
            UserPrompts.Add(userPrompt);
            string representative = Match(userPrompt, @"Reprezentant legal:\s*([^\r\n]+)", "[De completat: Reprezentant Legal]");
            string iban = Match(userPrompt, @"Cont IBAN:\s*([^\r\n]+)", "[De completat: Cont IBAN]");
            string expert = Match(userPrompt, @"Experți cheie:[\s\S]*?\n-\s*([^\r\n]+)", "[De completat: Expert cheie]");
            string json = "{" +
                "\"title\": \"Ofertă tehnică — Lucrări de construcții\"," +
                "\"technicalProposal\": {\"sections\": [" +
                "{\"heading\": \"Date administrative\", \"content\": \"Reprezentant legal: " + representative + "; Cont IBAN: " + iban + "\"}," +
                "{\"heading\": \"Resurse umane\", \"content\": \"Expert nominalizat: " + expert + "\"}," +
                "{\"heading\": \"Metodologie\", \"content\": \"Lucrările se execută conform normelor în vigoare.\"}" +
                "]}," +
                "\"billOfQuantities\": [{\"position\": 1, \"description\": \"Transport materiale\", \"unit\": \"buc\", \"quantity\": 2, \"unitPrice\": 250, \"total\": 500}]" +
                "}";
            return Task.FromResult(json);
        }

        private static string Match(string input, string pattern, string fallback)
        {
            Match match = Regex.Match(input, pattern, RegexOptions.Compiled);
            return match.Success ? match.Groups[1].Value.Trim() : fallback;
        }
    }
}
