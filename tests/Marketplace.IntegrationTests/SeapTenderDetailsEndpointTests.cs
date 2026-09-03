using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Marketplace.Application.Seap;
using Marketplace.Domain.Seap;
using Marketplace.Infrastructure.Bidding;
using Marketplace.Infrastructure.Persistence;
using Marketplace.Infrastructure.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit.Abstractions;

namespace Marketplace.IntegrationTests;

// The SEAP tender detail page reads GET /api/v1/seap/tenders/{id} (SeapTenderDetailsDto projection) and
// lets users download the original files the authority published via
// GET /api/v1/seap/tenders/{tenderId}/attachments/{attachmentId}. The attachment endpoint must stream the
// stored file server-side (browsers cannot navigate local:// or s3:// references) and must never serve an
// attachment that does not belong to the requested tender (IDOR guard).
[Collection(IntegrationTestGroup.Name)]
public sealed class SeapTenderDetailsEndpointTests : IntegrationTestBase, IClassFixture<MarketplaceWebApplicationFactory>
{
    private readonly MarketplaceWebApplicationFactory factory;
    private readonly ITestOutputHelper output;
    private static readonly JsonSerializerOptions ExampleJson = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public SeapTenderDetailsEndpointTests(MarketplaceWebApplicationFactory factory, ITestOutputHelper output)
    {
        this.factory = factory;
        this.output = output;
    }

    [Fact]
    public async Task TenderDetailsExposeTheProjectionWithDerivedFieldsAndAttachments()
    {
        using WebApplicationFactory<Program> host = CreateConfiguredHost();
        using HttpClient client = await CreateAuthenticatedClientAsync(host);

        (Guid tenderId, Guid attachmentId) = await SeedTenderWithAttachmentAsync(deadline: DateTimeOffset.UtcNow.AddDays(20));

        SeapTenderDetailsDto details = (await (await client.GetAsync($"/api/v1/seap/tenders/{tenderId}"))
            .Content.ReadFromJsonAsync<SeapTenderDetailsDto>())!;

        Assert.Equal("Caiet de sarcini pentru lucrări de construcții", details.Description);
        // The simplified-procedure external id prefix derives the procedure type deterministically.
        Assert.Equal("Procedură simplificată", details.ProcedureType);
        // The deadline is still in the future → the procedure is open.
        Assert.Equal("În desfășurare", details.ProcedureState);
        // This tender was seeded without an AI extraction, so the detail fields stay null and the SPA can
        // show its skeleton/placeholders until the summarizer runs at import.
        Assert.Null(details.AwardCriterion);
        Assert.Null(details.FundingSource);
        Assert.Null(details.ParticipationGuarantee);
        Assert.Null(details.SummaryPoints);

        SeapTenderAttachmentRef attachment = Assert.Single(details.Attachments);
        Assert.Equal(attachmentId, attachment.Id);
        Assert.Equal("fisa-date.pdf", attachment.FileName);
        Assert.Equal($"api/v1/seap/tenders/{tenderId}/attachments/{attachmentId}", attachment.DownloadUrl);
    }

    [Fact]
    public async Task AiExtractionFieldsAreExposedOnTheDetailProjection()
    {
        using WebApplicationFactory<Program> host = CreateConfiguredHost();
        using HttpClient client = await CreateAuthenticatedClientAsync(host);

        // The AI extraction fills Description (the search payload has no full text) and the detail fields.
        Guid tenderId = await SeedTenderAsync(DateTimeOffset.UtcNow.AddDays(20), description: null);
        await ApplyAiExtractionAsync(tenderId);

        SeapTenderDetailsDto details = (await (await client.GetAsync($"/api/v1/seap/tenders/{tenderId}"))
            .Content.ReadFromJsonAsync<SeapTenderDetailsDto>())!;

        // The AI extraction fills Description (no full notice text exists) and the decision-critical fields.
        Assert.Equal("Reabilitarea clădirii școlii din Chiajna.", details.Description);
        Assert.Equal("Prețul cel mai scăzut", details.AwardCriterion);
        Assert.Equal("120.000 RON", details.ParticipationGuarantee);
        Assert.Equal("Buget local", details.FundingSource);
        Assert.Equal("12 luni", details.ContractDuration);
        Assert.Equal(["Minim 2 contracte similare"], details.QualificationRequirements);
        Assert.Equal(["Manager de proiect"], details.KeyPersonnel);
        Assert.Equal(["ISO 9001, ISO 45001"], details.Certificates);
        Assert.Equal(["Garanție: 120.000 RON", "Experiență: minim 2 contracte"], details.SummaryPoints);
    }

    [Fact]
    public async Task SummaryIsGeneratedOnDemandForATenderWithoutOne()
    {
        using WebApplicationFactory<Program> host = CreateSummaryHost(new SummaryMockLlmProvider());
        using HttpClient client = await CreateAuthenticatedClientAsync(host);
        Guid tenderId = await SeedTenderAsync(DateTimeOffset.UtcNow.AddDays(20), description: null);

        SeapTenderDetailsDto details = (await (await client.PostAsJsonAsync($"/api/v1/seap/tenders/{tenderId}/summary", new { }))
            .Content.ReadFromJsonAsync<SeapTenderDetailsDto>())!;

        Assert.Equal("Prețul cel mai scăzut", details.AwardCriterion);
        Assert.Equal("120.000 RON", details.ParticipationGuarantee);
        Assert.Equal(["ISO 9001, ISO 45001"], details.Certificates);
        Assert.Equal(["Garanție de participare: 120.000 RON", "Termen de execuție: 12 luni"], details.SummaryPoints);
        // The AI 2–3 sentence summary fills the description because the search payload carries no full text.
        Assert.Equal("Reabilitarea clădirii școlii din Chiajna.", details.Description);
    }

    [Fact]
    public async Task SummaryEndpointGroundsTheSynthesisInTheDownloadedDocumentText()
    {
        RecordingSummaryLlmProvider llm = new();
        GroundingSeapApiClient client = new("Garanție de participare: 120.000 RON\nCriteriu de atribuire: prețul cel mai scăzut\nTermen de execuție: 12 luni");
        using WebApplicationFactory<Program> host = CreateSummaryDocumentHost(llm, client);
        using HttpClient http = await CreateAuthenticatedClientAsync(host);
        Guid tenderId = await SeedTenderAsync(DateTimeOffset.UtcNow.AddDays(20), description: null, noticeId: 900001, sysNoticeTypeId: 17);

        SeapTenderDetailsDto details = (await (await http.PostAsJsonAsync($"/api/v1/seap/tenders/{tenderId}/summary", new { }))
            .Content.ReadFromJsonAsync<SeapTenderDetailsDto>())!;

        // The published document was resolved, downloaded and its text fed into the generation...
        Assert.Single(details.Attachments);
        Assert.Null(details.SummaryDocumentNote);
        Assert.Contains("FisaDate.txt", llm.UserPrompt);
        Assert.Contains("Garanție de participare: 120.000 RON", llm.UserPrompt);
        Assert.Equal(["Garanție de participare: 120.000 RON", "Termen de execuție: 12 luni"], details.SummaryPoints);
        Assert.Equal(["ISO 9001, ISO 45001"], details.Certificates);
    }

    [Fact]
    public async Task SummaryEndpointFallsBackToMetadataAndNotifiesWhenDocumentsCannotBeResolved()
    {
        DocumentedSeapApiClient client = new() { ThrowOnFetch = true };
        using WebApplicationFactory<Program> host = CreateSummaryDocumentHost(new SummaryMockLlmProvider(), client);
        using HttpClient http = await CreateAuthenticatedClientAsync(host);
        Guid tenderId = await SeedTenderAsync(DateTimeOffset.UtcNow.AddDays(20), description: null, noticeId: 900001, sysNoticeTypeId: 17);

        SeapTenderDetailsDto details = (await (await http.PostAsJsonAsync($"/api/v1/seap/tenders/{tenderId}/summary", new { }))
            .Content.ReadFromJsonAsync<SeapTenderDetailsDto>())!;

        // The synthesis still ran from the notice metadata and the UI is explicitly notified.
        Assert.Empty(details.Attachments);
        Assert.Equal(["Garanție de participare: 120.000 RON", "Termen de execuție: 12 luni"], details.SummaryPoints);
        Assert.NotNull(details.SummaryDocumentNote);
        Assert.Contains("nu au putut fi descărcate automat", details.SummaryDocumentNote);
    }

    [Fact]
    public async Task RealExampleDetailPayloadFeedsTheFourTabs()
    {
        RecordingSummaryLlmProvider llm = new();
        GroundingSeapApiClient client = new(
            "FISA DE DATE — servicii de conservare\n" +
            "Criteriul de atribuire: prețul cel mai scăzut\n" +
            "Garanția de participare: 120.000 RON\n" +
            "Durata contractului: 12 luni\n" +
            "Sursa de finanțare: Buget local\n" +
            "Experiență similară: minim 2 contracte de construcții în ultimii 3 ani\n" +
            "Personal solicitat: Manager de proiect, Inginer RTE\n" +
            "Certificări: ISO 9001, ISO 45001, atestat ANRE");
        using WebApplicationFactory<Program> host = CreateSummaryDocumentHost(llm, client);
        using HttpClient http = await CreateAuthenticatedClientAsync(host);
        Guid tenderId = await SeedTenderAsync(DateTimeOffset.UtcNow.AddDays(20), description: null, noticeId: 900003, sysNoticeTypeId: 17);

        SeapTenderDetailsDto afterSummary = (await (await http.PostAsJsonAsync($"/api/v1/seap/tenders/{tenderId}/summary", new { }))
            .Content.ReadFromJsonAsync<SeapTenderDetailsDto>())!;
        SeapTenderDetailsDto fromDetailGet = (await (await http.GetAsync($"/api/v1/seap/tenders/{tenderId}"))
            .Content.ReadFromJsonAsync<SeapTenderDetailsDto>())!;

        string json = JsonSerializer.Serialize(fromDetailGet, ExampleJson);
        output.WriteLine(json);

        // Detalii Achiziție ← awardCriterion / fundingSource / contractDuration / description
        Assert.NotNull(fromDetailGet.Description);
        Assert.Equal("Prețul cel mai scăzut", fromDetailGet.AwardCriterion);
        Assert.Equal("Buget local", fromDetailGet.FundingSource);
        Assert.Equal("12 luni", fromDetailGet.ContractDuration);

        // Condiții de Calificare ← participationGuarantee / qualificationRequirements / keyPersonnel / certificates
        Assert.Equal("120.000 RON", fromDetailGet.ParticipationGuarantee);
        Assert.Equal(["Minim 2 contracte similare"], fromDetailGet.QualificationRequirements);
        Assert.Equal(["Manager de proiect"], fromDetailGet.KeyPersonnel);
        Assert.Equal(["ISO 9001, ISO 45001"], fromDetailGet.Certificates);

        // Documente Originale ← attachments
        Assert.Single(fromDetailGet.Attachments);

        // Sinteză & Ofertare AI ← summaryPoints
        Assert.Equal(["Garanție de participare: 120.000 RON", "Termen de execuție: 12 luni"], fromDetailGet.SummaryPoints);

        // The payload persisted through the real GET is identical to the POST /summary response.
        Assert.Equal(fromDetailGet.SummaryPoints, afterSummary.SummaryPoints);
    }

    [Fact]
    public async Task SummaryEndpointIsIdempotentAndDoesNotCallTheLlmAgain()
    {
        CountingSummaryLlmProvider llm = new();
        using WebApplicationFactory<Program> host = CreateSummaryHost(llm);
        using HttpClient client = await CreateAuthenticatedClientAsync(host);
        Guid tenderId = await SeedTenderAsync(DateTimeOffset.UtcNow.AddDays(20), description: null);

        SeapTenderDetailsDto first = (await (await client.PostAsJsonAsync($"/api/v1/seap/tenders/{tenderId}/summary", new { }))
            .Content.ReadFromJsonAsync<SeapTenderDetailsDto>())!;
        Assert.Equal(["Garanție de participare: 120.000 RON", "Termen de execuție: 12 luni"], first.SummaryPoints);
        Assert.Equal(1, llm.CallCount);

        // The second request returns the persisted summary without another LLM call.
        SeapTenderDetailsDto second = (await (await client.PostAsJsonAsync($"/api/v1/seap/tenders/{tenderId}/summary", new { }))
            .Content.ReadFromJsonAsync<SeapTenderDetailsDto>())!;
        Assert.Equal(first.SummaryPoints, second.SummaryPoints);
        Assert.Equal(1, llm.CallCount);
    }

    [Fact]
    public async Task SummaryEndpointEnrichesAnExistingMetadataOnlySummaryWhenDocumentsBecomeAvailable()
    {
        RecordingSummaryLlmProvider llm = new();
        GroundingSeapApiClient client = new(
            "Garanție de participare: 120.000 RON\nCriteriu de atribuire: prețul cel mai scăzut\nTermen de execuție: 12 luni");
        using WebApplicationFactory<Program> host = CreateSummaryDocumentHost(llm, client);
        using HttpClient http = await CreateAuthenticatedClientAsync(host);
        Guid tenderId = await SeedTenderAsync(DateTimeOffset.UtcNow.AddDays(20), description: null, noticeId: 900004, sysNoticeTypeId: 17);
        await ApplyAiExtractionAsync(tenderId); // a metadata-only summary already exists (grounded = false)

        SeapTenderDetailsDto details = (await (await http.PostAsJsonAsync($"/api/v1/seap/tenders/{tenderId}/summary", new { }))
            .Content.ReadFromJsonAsync<SeapTenderDetailsDto>())!;

        // The deliberate user action enriched the old metadata summary with the document text.
        Assert.Equal("Prețul cel mai scăzut", details.AwardCriterion);
        Assert.Equal("120.000 RON", details.ParticipationGuarantee);
        Assert.Equal(["ISO 9001, ISO 45001"], details.Certificates);
        Assert.Null(details.SummaryDocumentNote);
        Assert.Contains("Garanție de participare: 120.000 RON", llm.UserPrompt);
    }

    [Fact]
    public async Task SummaryEndpointKeepsAnExistingMetadataOnlySummaryWhenNoDocumentsCanBeFetched()
    {
        CountingSummaryLlmProvider llm = new();
        using WebApplicationFactory<Program> host = CreateSummaryDocumentHost(llm, new StubSeapApiClient());
        using HttpClient http = await CreateAuthenticatedClientAsync(host);
        Guid tenderId = await SeedTenderAsync(DateTimeOffset.UtcNow.AddDays(20), description: null, noticeId: 900005, sysNoticeTypeId: 17);
        await ApplyAiExtractionAsync(tenderId);

        SeapTenderDetailsDto details = (await (await http.PostAsJsonAsync($"/api/v1/seap/tenders/{tenderId}/summary", new { }))
            .Content.ReadFromJsonAsync<SeapTenderDetailsDto>())!;

        // Nothing could be enriched → the existing bullets are preserved and the UI is told why.
        Assert.Equal(0, llm.CallCount);
        Assert.Equal(["Garanție: 120.000 RON", "Experiență: minim 2 contracte"], details.SummaryPoints);
        Assert.NotNull(details.SummaryDocumentNote);
        Assert.Contains("nu au putut fi descărcate automat", details.SummaryDocumentNote);
    }

    [Fact]
    public async Task SummaryEndpointReturns503WithFriendlyDetailWhenTheLlmFails()
    {
        using WebApplicationFactory<Program> host = CreateSummaryHost(new ThrowingSummaryLlmProvider());
        using HttpClient client = await CreateAuthenticatedClientAsync(host);
        Guid tenderId = await SeedTenderAsync(DateTimeOffset.UtcNow.AddDays(20), description: null);

        HttpResponseMessage response = await client.PostAsJsonAsync($"/api/v1/seap/tenders/{tenderId}/summary", new { });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("Sinteza AI nu a putut fi generată", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task SummaryEndpointReturns404ForAnUnknownTender()
    {
        using WebApplicationFactory<Program> host = CreateSummaryHost(new SummaryMockLlmProvider());
        using HttpClient client = await CreateAuthenticatedClientAsync(host);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/seap/tenders/{Guid.NewGuid()}/summary", new { });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SummaryEndpointRequiresAuthentication()
    {
        using HttpClient client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        Guid tenderId = await SeedTenderAsync(DateTimeOffset.UtcNow.AddDays(20));

        HttpResponseMessage response = await client.PostAsJsonAsync($"/api/v1/seap/tenders/{tenderId}/summary", new { });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RemotePublishedDocumentStreamsThroughTheDownloadEndpoint()
    {
        StubSeapApiClient remoteClient = new();
        using WebApplicationFactory<Program> host = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ISeapApiClient>();
                services.AddSingleton<ISeapApiClient>(remoteClient);
            });
        });
        using HttpClient client = await CreateAuthenticatedClientAsync(host);

        (Guid tenderId, Guid attachmentId) = await SeedTenderWithRemoteAttachmentAsync();

        HttpResponseMessage download = await client.GetAsync($"/api/v1/seap/tenders/{tenderId}/attachments/{attachmentId}");

        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("application/pdf", download.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment", download.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal("remote-doc.pdf", download.Content.Headers.ContentDisposition?.FileName);
        Assert.Equal("remote-file-bytes"u8.ToArray(), await download.Content.ReadAsByteArrayAsync());
        // The remote reference is proxied from e-licitatie.ro (the client receives the absolute URL).
        Assert.Equal("https://e-licitatie.ro/api-pub/files/noticedoc/abc", remoteClient.RequestedUrl);
    }

    [Fact]
    public async Task PublishedDocumentsAreResolvedOnDemandForATenderWithANoticeId()
    {
        DocumentedSeapApiClient client = new();
        using WebApplicationFactory<Program> host = CreateDocumentHost(client);
        using HttpClient http = await CreateAuthenticatedClientAsync(host);
        Guid tenderId = await SeedTenderAsync(DateTimeOffset.UtcNow.AddDays(20), description: null, noticeId: 900001, sysNoticeTypeId: 17);

        SeapTenderDetailsDto details = (await (await http.PostAsJsonAsync($"/api/v1/seap/tenders/{tenderId}/documents", new { }))
            .Content.ReadFromJsonAsync<SeapTenderDetailsDto>())!;

        Assert.Equal(2, details.Attachments.Count);
        Assert.Contains(details.Attachments, attachment => attachment.FileName == "4. Caiet de sarcini sala sport.pdf.p7s");
        Assert.Contains(details.Attachments, attachment => attachment.FileName == "FisaDate.pdf");
        Assert.All(details.Attachments, attachment => Assert.StartsWith($"api/v1/seap/tenders/{tenderId}/attachments/", attachment.DownloadUrl));
        Assert.Equal(1, client.DocumentFetches);
    }

    [Fact]
    public async Task PublishedDocumentsEndpointIsIdempotentAndSkipsTendersWithoutANoticeId()
    {
        DocumentedSeapApiClient client = new();
        using WebApplicationFactory<Program> host = CreateDocumentHost(client);
        using HttpClient http = await CreateAuthenticatedClientAsync(host);

        // Tender with attachments already present → no fetch, no change.
        (Guid tenderWithDocs, _) = await SeedTenderWithAttachmentAsync(deadline: DateTimeOffset.UtcNow.AddDays(20));
        SeapTenderDetailsDto first = (await (await http.PostAsJsonAsync($"/api/v1/seap/tenders/{tenderWithDocs}/documents", new { }))
            .Content.ReadFromJsonAsync<SeapTenderDetailsDto>())!;
        Assert.Single(first.Attachments);
        Assert.Equal(0, client.DocumentFetches);

        // Tender without a persisted notice id → nothing to resolve yet (returns as-is, no fetch).
        Guid tenderWithoutId = await SeedTenderAsync(DateTimeOffset.UtcNow.AddDays(20), description: null);
        SeapTenderDetailsDto second = (await (await http.PostAsJsonAsync($"/api/v1/seap/tenders/{tenderWithoutId}/documents", new { }))
            .Content.ReadFromJsonAsync<SeapTenderDetailsDto>())!;
        Assert.Empty(second.Attachments);
        Assert.Equal(0, client.DocumentFetches);
    }

    [Fact]
    public async Task PublishedDocumentsEndpointReturns503WhenTheFetchFails()
    {
        DocumentedSeapApiClient client = new() { ThrowOnFetch = true };
        using WebApplicationFactory<Program> host = CreateDocumentHost(client);
        using HttpClient http = await CreateAuthenticatedClientAsync(host);
        Guid tenderId = await SeedTenderAsync(DateTimeOffset.UtcNow.AddDays(20), description: null, noticeId: 900001, sysNoticeTypeId: 17);

        HttpResponseMessage response = await http.PostAsJsonAsync($"/api/v1/seap/tenders/{tenderId}/documents", new { });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("Documentele anunțului nu au putut fi încărcate", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task OriginalAttachmentStreamsWithContentTypeAndFileName()
    {
        using WebApplicationFactory<Program> host = CreateConfiguredHost();
        using HttpClient client = await CreateAuthenticatedClientAsync(host);

        (Guid tenderId, Guid attachmentId) = await SeedTenderWithAttachmentAsync(deadline: DateTimeOffset.UtcNow.AddDays(5));

        HttpResponseMessage download = await client.GetAsync($"/api/v1/seap/tenders/{tenderId}/attachments/{attachmentId}");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("application/pdf", download.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment", download.Content.Headers.ContentDisposition?.DispositionType);
        Assert.EndsWith("fisa-date.pdf", download.Content.Headers.ContentDisposition?.FileName);
        byte[] bytes = await download.Content.ReadAsByteArrayAsync();
        Assert.Equal("fisa-date-pdf-content"u8.ToArray(), bytes);
    }

    [Fact]
    public async Task AttachmentDownloadDoesNotLeakAnotherTendersFiles()
    {
        using WebApplicationFactory<Program> host = CreateConfiguredHost();
        using HttpClient client = await CreateAuthenticatedClientAsync(host);

        (Guid tenderId, Guid attachmentId) = await SeedTenderWithAttachmentAsync(deadline: DateTimeOffset.UtcNow.AddDays(5));
        Guid otherTenderId = await SeedTenderAsync(DateTimeOffset.UtcNow.AddDays(5));

        // The attachment belongs to the first tender; requesting it through another tender must 404.
        HttpResponseMessage response =
            await client.GetAsync($"/api/v1/seap/tenders/{otherTenderId}/attachments/{attachmentId}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AttachmentDownloadRequiresAuthentication()
    {
        using HttpClient client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });

        (Guid tenderId, Guid attachmentId) = await SeedTenderWithAttachmentAsync(deadline: DateTimeOffset.UtcNow.AddDays(5));

        HttpResponseMessage response =
            await client.GetAsync($"/api/v1/seap/tenders/{tenderId}/attachments/{attachmentId}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private WebApplicationFactory<Program> CreateConfiguredHost() =>
        factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Features:AutoBiddingEnabled"] = "true",
                    ["AI:UseMockProvider"] = "true"
                }));
        });

    /// <summary>Host for the on-demand summary endpoint: the shared mock LLM is replaced with a provider that
    /// returns the summary JSON contract (the default StaticMockLlmProvider returns the proposal JSON, which
    /// the summarizer would correctly reject).</summary>
    private WebApplicationFactory<Program> CreateSummaryHost(ILlmProvider llm) =>
        factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["AI:UseMockProvider"] = "true" }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ILlmProvider>();
                services.AddSingleton(llm);
            });
        });

    /// <summary>Host for the summary endpoints that also replaces the SEAP client, so the document-grounding
    /// path (resolve published documents → download → read text) never hits the live platform.</summary>
    private WebApplicationFactory<Program> CreateSummaryDocumentHost(ILlmProvider llm, ISeapApiClient seap) =>
        factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["AI:UseMockProvider"] = "true" }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ILlmProvider>();
                services.AddSingleton(llm);
                services.RemoveAll<ISeapApiClient>();
                services.AddSingleton(seap);
            });
        });

    private static async Task<HttpClient> CreateAuthenticatedClientAsync(WebApplicationFactory<Program> host)
    {
        HttpClient client = host.CreateClient(new() { BaseAddress = new Uri("https://localhost") });

        string email = $"seap-details-{Guid.NewGuid():N}@test.local";
        const string password = "Test123456!";
        Assert.Equal(HttpStatusCode.Created,
            (await client.PostAsJsonAsync("/api/v1/auth/register/supplier", new { email, password })).StatusCode);
        TokenResponse token = (await (await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password }))
            .Content.ReadFromJsonAsync<TokenResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        return client;
    }

    private async Task<(Guid TenderId, Guid AttachmentId)> SeedTenderWithAttachmentAsync(DateTimeOffset deadline)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        MarketplaceDbContext db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
        IStorageService storage = scope.ServiceProvider.GetRequiredService<IStorageService>();
        SeapTender tender = SeapTender.Import(
            $"SCN{Guid.NewGuid():N}"[..12], "Lucrări de construcții grădiniță", "Caiet de sarcini pentru lucrări de construcții",
            ["45214100-1"], ["Lucrari de constructii de gradinite"], 2_420_466m, "RON",
            "Primaria Comunei Poiana Dambovita", "Dâmbovița",
            DateTimeOffset.UtcNow.AddDays(-2), deadline, DateTimeOffset.UtcNow);
        await using var content = new MemoryStream("fisa-date-pdf-content"u8.ToArray());
        string reference = await storage.UploadAsync(
            "seap", $"seap/{tender.Id}", "fisa-date.pdf", content, "application/pdf", CancellationToken.None);
        SeapTenderAttachment attachment = tender.AddAttachment("fisa-date.pdf", DateTimeOffset.UtcNow, storageReference: reference);
        db.SeapTenders.Add(tender);
        await db.SaveChangesAsync();
        return (tender.Id, attachment.Id);
    }

    private async Task<Guid> SeedTenderAsync(DateTimeOffset deadline, string? description = "Caiet de sarcini pentru lucrări de construcții", int? noticeId = null, int? sysNoticeTypeId = null)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        MarketplaceDbContext db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
        SeapTender tender = SeapTender.Import(
            $"SCN{Guid.NewGuid():N}"[..12], "Lucrări de construcții grădiniță", description,
            ["45214100-1"], ["Lucrari de constructii de gradinite"], 2_420_466m, "RON",
            "Primaria Comunei Poiana Dambovita", "Dâmbovița",
            DateTimeOffset.UtcNow.AddDays(-2), deadline, DateTimeOffset.UtcNow,
            noticeId, sysNoticeTypeId);
        db.SeapTenders.Add(tender);
        await db.SaveChangesAsync();
        return tender.Id;
    }

    private async Task ApplyAiExtractionAsync(Guid tenderId)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        MarketplaceDbContext db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
        SeapTender tender = db.SeapTenders.Single(item => item.Id == tenderId);
        tender.ApplyAiExtraction(new SeapAiExtraction(
            "Reabilitarea clădirii școlii din Chiajna.", "Prețul cel mai scăzut", "120.000 RON", "Buget local", "12 luni",
            "Minim 2 contracte similare", "Manager de proiect", "ISO 9001, ISO 45001",
            ["Garanție: 120.000 RON", "Experiență: minim 2 contracte"]), DateTimeOffset.UtcNow);
        await db.SaveChangesAsync();
    }

    /// <summary>Seeds a tender whose attachment is a remote e-licitatie.ro document reference (the
    /// published-document path the ingestion now resolves) instead of a local/S3 storage reference.</summary>
    private async Task<(Guid TenderId, Guid AttachmentId)> SeedTenderWithRemoteAttachmentAsync()
    {
        using IServiceScope scope = factory.Services.CreateScope();
        MarketplaceDbContext db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
        SeapTender tender = SeapTender.Import(
            $"SCN{Guid.NewGuid():N}"[..12], "Lucrări de construcții grădiniță", null,
            ["45214100-1"], ["Lucrari de constructii de gradinite"], 2_420_466m, "RON",
            "Primaria Comunei Poiana Dambovita", "Dâmbovița",
            DateTimeOffset.UtcNow.AddDays(-2), DateTimeOffset.UtcNow.AddDays(20), DateTimeOffset.UtcNow);
        SeapTenderAttachment attachment = tender.AddAttachment(
            "remote-doc.pdf", DateTimeOffset.UtcNow, sourceUrl: "https://e-licitatie.ro/api-pub/files/noticedoc/abc");
        db.SeapTenders.Add(tender);
        await db.SaveChangesAsync();
        return (tender.Id, attachment.Id);
    }

    /// <summary>Host for the on-demand document endpoint: the real SEAP client is replaced with a stub so
    /// the fetch never hits the live platform.</summary>
    private WebApplicationFactory<Program> CreateDocumentHost(ISeapApiClient client) =>
        factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ISeapApiClient>();
                services.AddSingleton(client);
            });
        });

    private sealed record TokenResponse(string AccessToken, DateTimeOffset ExpiresAt);

    /// <summary>Replaces the real SEAP client for the remote-document proxy test: records the requested URL
    /// and returns a fixed stream instead of hitting e-licitatie.ro.</summary>
    private sealed class StubSeapApiClient : ISeapApiClient
    {
        public string? RequestedUrl { get; private set; }

        public Task<IReadOnlyCollection<SeapTenderNoticeDto>> FetchRecentNoticesAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<SeapTenderNoticeDto>>([]);

        public Task<IReadOnlyCollection<SeapTenderNoticeDto>> FetchNoticePageAsync(
            int pageIndex, int pageSize, DateOnly? startPublicationDate, DateOnly? endPublicationDate,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<SeapTenderNoticeDto>>([]);

        public Task<SeapTenderNoticeDto?> FetchNoticeByNumberAsync(string noticeNo, CancellationToken cancellationToken) =>
            Task.FromResult<SeapTenderNoticeDto?>(null);

        public Task<IReadOnlyCollection<SeapNoticeDocumentDto>> FetchNoticeDocumentsAsync(
            int noticeId, int sysNoticeTypeId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<SeapNoticeDocumentDto>>([]);

        public Task<Stream> DownloadAttachmentAsync(string downloadUrl, CancellationToken cancellationToken)
        {
            RequestedUrl = downloadUrl;
            return Task.FromResult<Stream>(new MemoryStream("remote-file-bytes"u8.ToArray()));
        }
    }

    /// <summary>Replaces the real SEAP client for the on-demand document tests: counts the document fetches
    /// and returns the published-document list (or throws when <see cref="ThrowOnFetch"/> is set).</summary>
    private sealed class DocumentedSeapApiClient : ISeapApiClient
    {
        public int DocumentFetches { get; private set; }
        public bool ThrowOnFetch { get; init; }

        public Task<IReadOnlyCollection<SeapTenderNoticeDto>> FetchRecentNoticesAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<SeapTenderNoticeDto>>([]);

        public Task<IReadOnlyCollection<SeapTenderNoticeDto>> FetchNoticePageAsync(
            int pageIndex, int pageSize, DateOnly? startPublicationDate, DateOnly? endPublicationDate,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<SeapTenderNoticeDto>>([]);

        public Task<SeapTenderNoticeDto?> FetchNoticeByNumberAsync(string noticeNo, CancellationToken cancellationToken) =>
            Task.FromResult<SeapTenderNoticeDto?>(null);

        public Task<Stream> DownloadAttachmentAsync(string downloadUrl, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Attachments are disabled in this test.");

        public Task<IReadOnlyCollection<SeapNoticeDocumentDto>> FetchNoticeDocumentsAsync(
            int noticeId, int sysNoticeTypeId, CancellationToken cancellationToken)
        {
            DocumentFetches++;
            if (ThrowOnFetch) throw new InvalidOperationException("SEAP indisponibil.");
            return Task.FromResult<IReadOnlyCollection<SeapNoticeDocumentDto>>([
                new SeapNoticeDocumentDto("CN-X/00005", "4. Caiet de sarcini sala sport.pdf.p7s", "https://e-licitatie.ro/api-pub/files/noticedoc/aaa"),
                new SeapNoticeDocumentDto("CN-X/00009", "FisaDate.pdf", "https://e-licitatie.ro/api-pub/files/noticedoc/bbb")
            ]);
        }
    }

    private const string SummaryJson = """
    {
      "summaryPoints": ["Garanție de participare: 120.000 RON", "Termen de execuție: 12 luni"],
      "description": "Reabilitarea clădirii școlii din Chiajna.",
      "awardCriterion": "Prețul cel mai scăzut",
      "participationGuarantee": "120.000 RON",
      "fundingSource": "Buget local",
      "contractDuration": "12 luni",
      "qualificationRequirements": "Minim 2 contracte similare",
      "keyPersonnel": "Manager de proiect",
      "certificates": "ISO 9001, ISO 45001"
    }
    """;

    private sealed class SummaryMockLlmProvider : ILlmProvider
    {
        public Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken) =>
            Task.FromResult(SummaryJson);
    }

    private sealed class CountingSummaryLlmProvider : ILlmProvider
    {
        public int CallCount { get; private set; }

        public Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(SummaryJson);
        }
    }

    private sealed class ThrowingSummaryLlmProvider : ILlmProvider
    {
        public Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("LLM indisponibil.");
    }

    /// <summary>LLM stub that records the last user prompt so tests can assert the document text was grounded.</summary>
    private sealed class RecordingSummaryLlmProvider : ILlmProvider
    {
        public string UserPrompt { get; private set; } = string.Empty;

        public Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken)
        {
            UserPrompt = userPrompt;
            return Task.FromResult(SummaryJson);
        }
    }

    /// <summary>SEAP client stub for the grounded-summary test: returns one published .txt document and
    /// streams its content, so the reader can feed the text into the LLM prompt.</summary>
    private sealed class GroundingSeapApiClient(string documentText) : ISeapApiClient
    {
        public Task<IReadOnlyCollection<SeapTenderNoticeDto>> FetchRecentNoticesAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<SeapTenderNoticeDto>>([]);

        public Task<IReadOnlyCollection<SeapTenderNoticeDto>> FetchNoticePageAsync(
            int pageIndex, int pageSize, DateOnly? startPublicationDate, DateOnly? endPublicationDate,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<SeapTenderNoticeDto>>([]);

        public Task<SeapTenderNoticeDto?> FetchNoticeByNumberAsync(string noticeNo, CancellationToken cancellationToken) =>
            Task.FromResult<SeapTenderNoticeDto?>(null);

        public Task<Stream> DownloadAttachmentAsync(string downloadUrl, CancellationToken cancellationToken) =>
            Task.FromResult<Stream>(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(documentText)));

        public Task<IReadOnlyCollection<SeapNoticeDocumentDto>> FetchNoticeDocumentsAsync(
            int noticeId, int sysNoticeTypeId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<SeapNoticeDocumentDto>>([
                new SeapNoticeDocumentDto("CN-X/1", "FisaDate.txt", "https://e-licitatie.ro/api-pub/files/fisa-date.txt")
            ]);
    }
}
