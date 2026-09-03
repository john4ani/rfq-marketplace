using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Marketplace.Application.Seap;
using Marketplace.Domain.Seap;
using Marketplace.Infrastructure.Bidding;
using Marketplace.Infrastructure.Persistence;
using Marketplace.Infrastructure.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Marketplace.IntegrationTests;

// Livrarea 1 — „Direct Hit" production-safe resolve: POST /api/v1/seap/tenders/resolve must
//   • return HTTP 200 with the detail DTO only when the local tender already has a document-grounded analysis
//     (no external calls at all);
//   • never re-search SEAP for a locally-known tender that is merely not analyzed yet (existing NoticeId kept,
//     one ingestion run ensured, HTTP 202);
//   • on a genuine local miss query SEAP with an exact notice-number match, upsert metadata + NoticeId on the
//     unique normalized id and start an ingestion run (HTTP 202);
//   • import an expired notice as DirectHistoricalLookup that never becomes an active bid target;
//   • answer 404 only when SEAP answered and has no exact match, and 502 when the platform is unavailable.
// GET /api/v1/seap/tenders/{id}/ingestion-status exposes the persisted run/tender state.
// Documents and the LLM are deliberately NOT part of resolve — they belong to the worker delivery.
[Collection(IntegrationTestGroup.Name)]
public sealed class SeapDirectHitEndpointTests : IntegrationTestBase, IClassFixture<MarketplaceWebApplicationFactory>
{
    private readonly MarketplaceWebApplicationFactory factory;

    public SeapDirectHitEndpointTests(MarketplaceWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task ResolveIsUnavailable503WhenTheDirectHitFlagIsOffAndCreatesNothing()
    {
        DirectHitSeapApiClient client = new(null);
        using WebApplicationFactory<Program> host = CreateDirectHitHost(new StubLlmProvider(), client, directHitEnabled: false);
        using HttpClient http = await CreateAuthenticatedClientAsync(host);

        HttpResponseMessage response = await http.PostAsJsonAsync("/api/v1/seap/tenders/resolve", new { noticeNo = "SCN7777000" });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        // No tender and no ingestion run were created — the gate is checked before any work happens.
        await using MarketplaceDbContext db = new(TestDatabase.CreateOptions());
        Assert.Equal(0, await db.SeapTenders.CountAsync(tender => tender.NormalizedExternalId == "SCN7777000"));
        Assert.Equal(0, await db.SeapIngestionRuns.CountAsync(run => run.NoticeNo == "SCN7777000"));
    }

    [Fact]
    public async Task ResolveIsUnavailable503WhenOnlyTheDirectHitFlagIsOnButTheWorkerIsOff()
    {
        // SeapDirectHitEnabled=true + SeapIngestionWorkerEnabled=false must NOT create Pending runs that
        // nobody processes — resolve answers 503 before any write.
        DirectHitSeapApiClient client = new(null);
        using WebApplicationFactory<Program> host = CreateDirectHitHost(new StubLlmProvider(), client, directHitEnabled: true, workerEnabled: false);
        using HttpClient http = await CreateAuthenticatedClientAsync(host);

        HttpResponseMessage response = await http.PostAsJsonAsync("/api/v1/seap/tenders/resolve", new { noticeNo = "SCN7777001" });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        await using MarketplaceDbContext db = new(TestDatabase.CreateOptions());
        Assert.Equal(0, await db.SeapTenders.CountAsync(tender => tender.NormalizedExternalId == "SCN7777001"));
        Assert.Equal(0, await db.SeapIngestionRuns.CountAsync(run => run.NoticeNo == "SCN7777001"));
    }

    [Fact]
    public async Task ResolveReturns200ReadyLocalTenderWithoutAnyExternalCall()
    {
        DirectHitSeapApiClient client = new(null);
        using WebApplicationFactory<Program> host = CreateDirectHitHost(new StubLlmProvider(), client);
        using HttpClient http = await CreateAuthenticatedClientAsync(host);
        Guid tenderId = await SeedGroundedTenderAsync("SCN1179200", noticeId: 900_100);

        HttpResponseMessage response = await http.PostAsJsonAsync("/api/v1/seap/tenders/resolve", new { noticeNo = "scn1179200" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        ResolveEnvelope envelope = (await response.Content.ReadFromJsonAsync<ResolveEnvelope>())!;

        Assert.Equal("SCN1179200", envelope.NoticeNo);
        Assert.Equal("Ready", envelope.Status);
        Assert.Null(envelope.RunId);
        Assert.NotNull(envelope.Tender);
        Assert.Equal(900_100, envelope.Tender.NoticeId);
        Assert.True(envelope.Tender.SummaryGroundedInDocuments);
        Assert.Single(envelope.Tender.Attachments);
        // Local ready hit → no live lookup, no run.
        Assert.Equal(0, client.NoticeLookups);

        // The ingestion-status endpoint reports Ready.
        SeapIngestionStatusDto status = (await (await http.GetAsync($"/api/v1/seap/tenders/{tenderId}/ingestion-status"))
            .Content.ReadFromJsonAsync<SeapIngestionStatusDto>())!;
        Assert.Equal("Ready", status.Status);
        Assert.Equal("Standard", status.ImportMode);
    }

    [Fact]
    public async Task ResolveMissUpsertsMetadataStartsRunAndTracksPendingStatus()
    {
        DirectHitSeapApiClient client = new(OpenNotice("SCN1179200", noticeId: 900_100));
        using WebApplicationFactory<Program> host = CreateDirectHitHost(new StubLlmProvider(), client);
        using HttpClient http = await CreateAuthenticatedClientAsync(host);

        HttpResponseMessage response = await http.PostAsJsonAsync("/api/v1/seap/tenders/resolve", new { noticeNo = "SCN1179200" });
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        ResolveEnvelope envelope = (await response.Content.ReadFromJsonAsync<ResolveEnvelope>())!;
        Assert.Equal("Pending", envelope.Status);
        Assert.NotNull(envelope.RunId);

        await using (MarketplaceDbContext db = CreateDatabase())
        {
            SeapTender saved = await db.SeapTenders
                .Include(tender => tender.Attachments)
                .SingleAsync(tender => tender.NormalizedExternalId == "SCN1179200");
            Assert.Equal(900_100, saved.NoticeId);
            Assert.Equal(SeapImportMode.OnDemand, saved.ImportMode);
            Assert.Equal(SeapTenderStatus.Imported, saved.Status);
            Assert.Equal("SCN1179200", saved.NormalizedExternalId);
            // Livrarea 1 deliberately performs NO document download and NO LLM analysis inside resolve.
            Assert.Empty(saved.Attachments);
            Assert.False(saved.SummaryGroundedInDocuments);
            Assert.Equal(1, await db.SeapIngestionRuns.CountAsync(run => run.TenderId == saved.Id && run.Status == SeapIngestionStatus.Pending));
        }

        SeapIngestionStatusDto status = (await (await http.GetAsync($"/api/v1/seap/tenders/{envelope.TenderId}/ingestion-status"))
            .Content.ReadFromJsonAsync<SeapIngestionStatusDto>())!;
        Assert.Equal("Pending", status.Status);
        Assert.Equal("SCN1179200", status.NoticeNo);
    }

    [Fact]
    public async Task ResolveLocalNotReadyNeverReSearchesSeapAndKeepsASingleRun()
    {
        DirectHitSeapApiClient client = new(null);
        using WebApplicationFactory<Program> host = CreateDirectHitHost(new StubLlmProvider(), client);
        using HttpClient http = await CreateAuthenticatedClientAsync(host);
        // Local row exists with a persisted numeric NoticeId but no analysis yet (legacy/lean import).
        await SeedTenderAsync("SCN888001", noticeId: 900_200);

        HttpResponseMessage first = await http.PostAsJsonAsync("/api/v1/seap/tenders/resolve", new { noticeNo = "SCN888001" });
        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        HttpResponseMessage second = await http.PostAsJsonAsync("/api/v1/seap/tenders/resolve", new { noticeNo = "scn888001" });
        Assert.Equal(HttpStatusCode.Accepted, second.StatusCode);

        // Two resolves of the same local tender → zero live SEAP lookups and exactly one active run.
        Assert.Equal(0, client.NoticeLookups);
        await using (MarketplaceDbContext db = CreateDatabase())
        {
            Guid tenderId = (await db.SeapTenders.SingleAsync(tender => tender.NormalizedExternalId == "SCN888001")).Id;
            Assert.Equal(1, await db.SeapIngestionRuns.CountAsync(run => run.TenderId == tenderId));
            Assert.Equal(1, await db.SeapIngestionRuns.CountAsync(run => run.TenderId == tenderId && run.Status == SeapIngestionStatus.Pending));
        }
    }

    [Fact]
    public async Task ResolveExpiredNoticeIsImportedAsDirectHistoricalLookupNotAnActiveTarget()
    {
        DirectHitSeapApiClient client = new(OpenNotice("SCN1179100", noticeId: 900_300, deadlineOffsetDays: -5));
        using WebApplicationFactory<Program> host = CreateDirectHitHost(new StubLlmProvider(), client);
        using HttpClient http = await CreateAuthenticatedClientAsync(host);

        HttpResponseMessage response = await http.PostAsJsonAsync("/api/v1/seap/tenders/resolve", new { noticeNo = "SCN1179100" });
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        await using (MarketplaceDbContext db = CreateDatabase())
        {
            SeapTender saved = await db.SeapTenders.SingleAsync(tender => tender.SeapExternalId == "SCN1179100");
            Assert.Equal(SeapImportMode.DirectHistoricalLookup, saved.ImportMode);
            // Research history never becomes a bid target: stays Imported, not ReadyForBidding.
            Assert.Equal(SeapTenderStatus.Imported, saved.Status);
        }
    }

    [Fact]
    public async Task ResolveReturns404OnlyWhenSeapDoesNotKnowTheNumber()
    {
        DirectHitSeapApiClient client = new(null);
        using WebApplicationFactory<Program> host = CreateDirectHitHost(new StubLlmProvider(), client);
        using HttpClient http = await CreateAuthenticatedClientAsync(host);

        HttpResponseMessage response = await http.PostAsJsonAsync("/api/v1/seap/tenders/resolve", new { noticeNo = "SCN9999999" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains("SCN9999999", body);
        Assert.Contains("nu a fost găsit", body);
        Assert.Equal(1, client.NoticeLookups);
    }

    [Fact]
    public async Task ResolveReturns502WhenTheSeapPlatformIsUnavailable()
    {
        DirectHitSeapApiClient client = new(OpenNotice("SCN1179200", noticeId: 900_100)) { ThrowOnLookup = true };
        using WebApplicationFactory<Program> host = CreateDirectHitHost(new StubLlmProvider(), client);
        using HttpClient http = await CreateAuthenticatedClientAsync(host);

        HttpResponseMessage response = await http.PostAsJsonAsync("/api/v1/seap/tenders/resolve", new { noticeNo = "SCN1179200" });

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains("SEAP", body);
        Assert.Contains("Încearcă din nou", body);
    }

    [Fact]
    public async Task ResolveRejectsAMalformedNoticeNumberWith400()
    {
        DirectHitSeapApiClient client = new(null);
        using WebApplicationFactory<Program> host = CreateDirectHitHost(new StubLlmProvider(), client);
        using HttpClient http = await CreateAuthenticatedClientAsync(host);

        HttpResponseMessage response = await http.PostAsJsonAsync("/api/v1/seap/tenders/resolve", new { noticeNo = "not-a-number" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, client.NoticeLookups);
    }

    private static SeapTenderNoticeDto ExpiredNotice() => OpenNotice("SCN1179100", noticeId: 900_300, deadlineOffsetDays: -5);

    private static SeapTenderNoticeDto OpenNotice(string id, int noticeId, int deadlineOffsetDays = 20) => new(
        id, "Reparații la clădirea școlii din Poiana", null,
        ["45214100-1"], 2_420_466m, "RON", "Primaria Comunei Poiana Dambovita",
        DateTimeOffset.UtcNow.AddDays(-2), DateTimeOffset.UtcNow.AddDays(deadlineOffsetDays), [],
        CpvNames: ["Lucrari de constructii de gradinite"], County: "Dâmbovița",
        NoticeId: noticeId, SysNoticeTypeId: 17);

    private WebApplicationFactory<Program> CreateDirectHitHost(
        ILlmProvider llm, ISeapApiClient seap, bool directHitEnabled = true, bool workerEnabled = true) =>
        factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Features:AutoBiddingEnabled"] = "true",
                    ["Features:SeapDirectHitEnabled"] = directHitEnabled ? "true" : "false",
                    ["Features:SeapIngestionWorkerEnabled"] = workerEnabled ? "true" : "false",
                    ["AI:UseMockProvider"] = "true"
                }));
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
        string email = $"seap-direct-{Guid.NewGuid():N}@test.local";
        const string password = "Test123456!";
        Assert.Equal(HttpStatusCode.Created,
            (await client.PostAsJsonAsync("/api/v1/auth/register/supplier", new { email, password })).StatusCode);
        TokenResponse token = (await (await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password }))
            .Content.ReadFromJsonAsync<TokenResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        return client;
    }

    private async Task<Guid> SeedGroundedTenderAsync(string externalId, int noticeId)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        MarketplaceDbContext db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
        IStorageService storage = scope.ServiceProvider.GetRequiredService<IStorageService>();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        SeapTender tender = SeapTender.Import(
            externalId, "Reparații la clădirea școlii din Poiana", "Reparații interioare și exterioare la clădirea școlii.",
            ["45214100-1"], ["Lucrari de constructii de gradinite"], 2_420_466m, "RON",
            "Primaria Comunei Poiana Dambovita", "Dâmbovița",
            now.AddDays(-2), now.AddDays(20), now, noticeId, 17);
        await using Stream content = new MemoryStream("fisa-date-pdf-content"u8.ToArray());
        string reference = await storage.UploadAsync("seap", $"seap/{tender.Id}", "FisaDate.pdf", content, "application/pdf", CancellationToken.None);
        tender.AddAttachment("FisaDate.pdf", now, storageReference: reference);
        tender.RecordDocumentGroundedAnalysis(new SeapAiExtraction(
            "Reparații la clădirea școlii din Poiana.", "Prețul cel mai scăzut", "120.000 RON", "Buget local",
            "12 luni", "Minim 2 contracte similare", "Manager de proiect", "ISO 9001, ISO 45001",
            ["Garanție de participare: 120.000 RON"]), "seed-doc-hash-1", 1, now);
        db.SeapTenders.Add(tender);
        await db.SaveChangesAsync();
        return tender.Id;
    }

    private async Task<Guid> SeedTenderAsync(string externalId, int? noticeId)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        MarketplaceDbContext db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
        SeapTender tender = SeapTender.Import(
            externalId, "Lucrări de construcții grădiniță", "Caiet de sarcini pentru lucrări de construcții",
            ["45214100-1"], ["Lucrari de constructii de gradinite"], 2_420_466m, "RON",
            "Primaria Comunei Poiana Dambovita", "Dâmbovița",
            DateTimeOffset.UtcNow.AddDays(-2), DateTimeOffset.UtcNow.AddDays(20), DateTimeOffset.UtcNow,
            noticeId, noticeId is null ? null : 17);
        db.SeapTenders.Add(tender);
        await db.SaveChangesAsync();
        return tender.Id;
    }

    private static MarketplaceDbContext CreateDatabase() => new(TestDatabase.CreateOptions());

    private sealed record ResolveEnvelope(Guid TenderId, string NoticeNo, string Status, Guid? RunId, SeapTenderDetailsDto? Tender = null);

    private sealed record TokenResponse(string AccessToken, DateTimeOffset ExpiresAt);

    private const string SummaryJson = """
    {
      "summaryPoints": ["Garanție de participare: 120.000 RON"],
      "description": "Reparații la clădirea școlii din Poiana.",
      "awardCriterion": "Prețul cel mai scăzut",
      "participationGuarantee": "120.000 RON",
      "fundingSource": "Buget local",
      "contractDuration": "12 luni",
      "qualificationRequirements": "Minim 2 contracte similare",
      "keyPersonnel": "Manager de proiect",
      "certificates": "ISO 9001, ISO 45001"
    }
    """;

    /// <summary>Placeholder LLM used to replace the real provider in the test hosts (resolve itself never
    /// calls the LLM in Livrarea 1 — this only keeps the default mock deterministic if the background
    /// ingestion worker runs during a test).</summary>
    private sealed class StubLlmProvider : ILlmProvider
    {
        public Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken) =>
            Task.FromResult(SummaryJson);
    }

    /// <summary>Live SEAP client stub for the resolve tests: counts notice-number lookups, returns the
    /// configured notice (or null / throws for the 404 and 502 tests) and never serves documents in Livrarea 1
    /// (resolve performs no document resolution).</summary>
    private sealed class DirectHitSeapApiClient(SeapTenderNoticeDto? notice) : ISeapApiClient
    {
        public int NoticeLookups { get; private set; }
        public bool ThrowOnLookup { get; init; }

        public Task<IReadOnlyCollection<SeapTenderNoticeDto>> FetchRecentNoticesAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<SeapTenderNoticeDto>>([]);

        public Task<IReadOnlyCollection<SeapTenderNoticeDto>> FetchNoticePageAsync(
            int pageIndex, int pageSize, DateOnly? startPublicationDate, DateOnly? endPublicationDate,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<SeapTenderNoticeDto>>([]);

        public Task<SeapTenderNoticeDto?> FetchNoticeByNumberAsync(string noticeNo, CancellationToken cancellationToken)
        {
            NoticeLookups++;
            if (ThrowOnLookup)
                throw new SeapSourceUnavailableException("Platforma SEAP a răspuns cu 403 la interogarea directă a unui anunț.");
            return Task.FromResult(notice);
        }

        public Task<Stream> DownloadAttachmentAsync(string downloadUrl, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Livrarea 1 resolve performs no document downloads.");

        public Task<IReadOnlyCollection<SeapNoticeDocumentDto>> FetchNoticeDocumentsAsync(
            int noticeId, int sysNoticeTypeId, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Livrarea 1 resolve performs no document resolution.");
    }
}