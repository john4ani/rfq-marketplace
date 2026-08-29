using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Marketplace.Infrastructure.Documents;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Marketplace.IntegrationTests;

// The auto-bid pipeline persists generated .docx/.xlsx as storage references (local://… in
// Development). The download endpoint must resolve those references server-side and stream the bytes
// with an attachment disposition — browsers cannot navigate local:///s3:// schemes directly.
[Collection(IntegrationTestGroup.Name)]
public sealed class CompanyVaultDownloadEndpointTests : IClassFixture<MarketplaceWebApplicationFactory>
{
    private readonly MarketplaceWebApplicationFactory factory;
    public CompanyVaultDownloadEndpointTests(MarketplaceWebApplicationFactory factory) => this.factory = factory;

    [Fact]
    public async Task GeneratedProposalFilesCanBeDownloadedAfterThePipelineRuns()
    {
        using WebApplicationFactory<Program> downloadFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IDocumentExtractionService>();
                services.AddSingleton<IDocumentExtractionService, FakeDocumentExtractionService>();
            });
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["AI:UseMockProvider"] = "true" }));
        });
        using HttpClient client = downloadFactory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });

        string email = $"vault-download-{Guid.NewGuid():N}@test.local";
        const string password = "Test123456!";
        Assert.Equal(HttpStatusCode.Created,
            (await client.PostAsJsonAsync("/api/v1/auth/register/supplier", new { email, password })).StatusCode);
        TokenResponse token = (await (await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password }))
            .Content.ReadFromJsonAsync<TokenResponse>())!;
        Guid userId = UserId(token.AccessToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        Assert.Equal(HttpStatusCode.OK,
            (await client.PutAsJsonAsync($"/api/v1/suppliers/{userId}", SupplierProfileRequest(email))).StatusCode);

        // Run the full pipeline: upload → (fake) extraction → mock LLM proposal → .docx/.xlsx stored locally.
        using var fileContent = new ByteArrayContent("caiet de sarcini de test"u8.ToArray());
        using var form = new MultipartFormDataContent();
        form.Add(fileContent, "file", "caiet-de-sarcini.pdf");
        HttpResponseMessage processed = await client.PostAsync($"/api/v1/companies/{userId}/vault/specifications", form);
        Assert.Equal(HttpStatusCode.OK, processed.StatusCode);
        SpecificationBidResult result = (await processed.Content.ReadFromJsonAsync<SpecificationBidResult>())!;
        Assert.False(string.IsNullOrWhiteSpace(result.TechnicalProposalUrl));
        Assert.False(string.IsNullOrWhiteSpace(result.BillOfQuantitiesUrl));

        await AssertDownloadAsync(client, userId, result.DocumentId, "TechnicalProposal", ".docx",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document");
        await AssertDownloadAsync(client, userId, result.DocumentId, "BillOfQuantities", ".xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");

        // A second upload for the SAME company must also work (regression: the existing-vault path used
        // to fail with DbUpdateConcurrencyException because the new document was tracked as Modified).
        using var secondContent = new ByteArrayContent("al doilea caiet de sarcini"u8.ToArray());
        using var secondForm = new MultipartFormDataContent();
        secondForm.Add(secondContent, "file", "caiet-de-sarcini-2.pdf");
        HttpResponseMessage reprocessed = await client.PostAsync($"/api/v1/companies/{userId}/vault/specifications", secondForm);
        if (!reprocessed.IsSuccessStatusCode)
            throw new Xunit.Sdk.XunitException(
                $"second upload failed: {(await reprocessed.Content.ReadAsStringAsync())}");
        Assert.Equal(HttpStatusCode.OK, reprocessed.StatusCode);
        SpecificationBidResult secondResult = (await reprocessed.Content.ReadFromJsonAsync<SpecificationBidResult>())!;
        Assert.NotEqual(result.DocumentId, secondResult.DocumentId);
    }

    private static async Task AssertDownloadAsync(
        HttpClient client, Guid userId, Guid documentId, string kind, string extension, string contentType)
    {
        HttpResponseMessage download = await client.GetAsync(
            $"/api/v1/companies/{userId}/vault/documents/{documentId}/file?kind={kind}");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal(contentType, download.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment", download.Content.Headers.ContentDisposition?.DispositionType);
        Assert.EndsWith(extension, download.Content.Headers.ContentDisposition?.FileName);
        byte[] bytes = await download.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 100, $"{kind} should not be empty");
        // .docx/.xlsx are ZIP archives and start with the PK magic bytes.
        Assert.Equal((byte)'P', bytes[0]);
        Assert.Equal((byte)'K', bytes[1]);
    }

    private static object SupplierProfileRequest(string email) => new
    {
        tradingName = "Companie descărcare test", companyRegistrationNumber = $"J00/{Guid.NewGuid():N}", taxIdentifier = "RO123",
        contactPerson = "Owner test", contactEmail = email, phoneNumber = "0700000000", website = (string?)null,
        address = "Strada Test 1", city = "Iași", county = "Iași", latitude = (decimal?)null,
        longitude = (decimal?)null, description = "Profil pentru testarea descărcării propunerilor generate"
    };

    private static Guid UserId(string accessToken)
    {
        string payloadPart = accessToken.Split('.')[1];
        string payload = Encoding.UTF8.GetString(Convert.FromBase64String(
            payloadPart.PadRight(payloadPart.Length + (4 - payloadPart.Length % 4) % 4, '=').Replace('-', '+').Replace('_', '/')));
        return Guid.Parse(JsonDocument.Parse(payload).RootElement.GetProperty("sub").GetString()!);
    }

    private sealed record TokenResponse(string AccessToken, DateTimeOffset ExpiresAt);
    private sealed record SpecificationBidResult(
        Guid DocumentId, string FileName, string DocumentUrl, string Markdown, string ProposalTitle,
        string TechnicalProposalUrl, string BillOfQuantitiesUrl, bool ShadowMode, DateTimeOffset GeneratedAt);

    private sealed class FakeDocumentExtractionService : IDocumentExtractionService
    {
        public Task<DocumentExtractionResult> ExtractAsync(Stream document, string fileName, CancellationToken cancellationToken) =>
            Task.FromResult(new DocumentExtractionResult(LongText, [new ExtractedPage(1, LongText, [])]));
    }

    /// <summary>OCR output long enough to pass the 200-char pipeline guard.</summary>
    private const string LongText =
        "Caiet de sarcini: executie lucrari de constructii la scoala din Chiajna, 12 luni, valoare estimata 1.500.000 RON. " +
        "Cerinte tehnice: organizare de santier conform legislatiei, executie conform proiectului si normelor in vigoare, " +
        "materiale certificate cu documente de provenienta, supraveghere permanenta a lucrarilor de catre dirigintele de santier, " +
        "receptia finala dupa verificarea tuturor fazelor de executie.";
}
