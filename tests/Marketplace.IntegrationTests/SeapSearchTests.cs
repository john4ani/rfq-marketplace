using Marketplace.Application.Seap;
using Marketplace.Domain.Seap;
using Marketplace.Infrastructure.Persistence;
using Marketplace.Infrastructure.Seap;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Marketplace.IntegrationTests;

// On-demand „Caută în SEAP": the search takes the active CPV/county parameters, pages the live SEAP
// search (honoring the publication-date window), derives each notice's county deterministically, imports
// only the matching notices (no LLM, no attachments), and returns them — bypassing the background
// hourly import cap because it is a user-initiated action.
[Collection(IntegrationTestGroup.Name)]
public sealed class SeapSearchTests : IntegrationTestBase
{
    [Fact]
    public async Task SearchAndImportTakesParametersAndAddsMatchingNoticesFromThePlatform()
    {
        DbContextOptions<MarketplaceDbContext> options = TestDatabase.CreateOptions();
        await using MarketplaceDbContext db = new(options);
        await db.Database.MigrateAsync();

        DateTimeOffset now = DateTimeOffset.UtcNow;
        // Page 0 mixes a matching Galați building notice with a Cluj one; page 1 adds a second Galați match.
        StubSeapApiClient api = new(
        [
            new SeapTenderNoticeDto("CN-SRCH-GL1", "Construcție școală Galați", null, ["45214200-2"], 1000m, "RON",
                "PRIMARIA GALATI", now, now.AddDays(30), [], County: "Galați"),
            new SeapTenderNoticeDto("CN-SRCH-CJ", "Medicamente Cluj", null, ["33652100-6"], 500m, "RON",
                "SPITAL CLUJ", now, now.AddDays(30), [], County: "Cluj")
        ],
        [
            new SeapTenderNoticeDto("CN-SRCH-GL2", "Construcție spital Galați", null, ["45215120-4"], 2000m, "RON",
                "CONSILIUL JUDETEAN GALATI", now, now.AddDays(30), [], County: "Galați")
        ]);

        SeapIngestionService service = new(db, api, new StubAttachmentStorage(), new NullSeapCategorizer(), new NullSeapSummaryGenerator(),
            Options.Create(new SeapOptions()), new FakeTimeProvider(), NullLogger<SeapIngestionService>.Instance);

        // „Caută în SEAP": buildings class 4521 within Galați — only the two matching notices are imported.
        IReadOnlyCollection<SeapTender> imported = await service.SearchAndImportAsync(
            ["45210000"], ["Galați"], new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), CancellationToken.None);

        Assert.Equal(["CN-SRCH-GL1", "CN-SRCH-GL2"],
            imported.Select(tender => tender.SeapExternalId).OrderBy(id => id).ToArray());
        Assert.Equal(2, await db.SeapTenders.CountAsync());

        // The matching tenders carry the derived county and the granular CPV code.
        SeapTender saved = await db.SeapTenders.SingleAsync(tender => tender.SeapExternalId == "CN-SRCH-GL1");
        Assert.Equal("Galați", saved.County);
        Assert.Contains("45214200-2", saved.CpvCodes);

        // The wide publication-date window is chunked per calendar month so the platform's ~3000-notice cap
        // cannot hide a whole year: the request is paged month by month (Jan…Dec 2026), plus the trailing
        // 30-days complement that guards a capped single month.
        Assert.Contains(api.Windows, window => window == (new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31)));
        Assert.Contains(api.Windows, window => window == (new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31)));
        Assert.Contains(api.Windows, window => window == (new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 31)));
        DateOnly today = DateOnly.FromDateTime(now.UtcDateTime);
        Assert.Contains((today.AddDays(-30), today), api.Windows);

        // A second search for the same parameters skips duplicates (already imported).
        IReadOnlyCollection<SeapTender> second = await service.SearchAndImportAsync(
            ["45210000"], ["Galați"], null, null, CancellationToken.None);
        Assert.Empty(second);
        Assert.Equal(2, await db.SeapTenders.CountAsync());
    }

    [Fact]
    public async Task SearchAlsoScansRecentWindowWhenTheRequestedWindowMissesRecentNotices()
    {
        DbContextOptions<MarketplaceDbContext> options = TestDatabase.CreateOptions();
        await using MarketplaceDbContext db = new(options);
        await db.Database.MigrateAsync();

        DateTimeOffset now = DateTimeOffset.UtcNow;
        DateOnly today = DateOnly.FromDateTime(now.UtcDateTime);
        // The requested (current-year) window returns nothing — mirroring the live observation where
        // „SCN1179140" (Brașov, 45453000-7) was missing from the capped current-year slice but present in
        // the uncapped trailing-30-days window. The recent complement must recover it.
        StubRecentWindowApiClient api = new(
            [new SeapTenderNoticeDto("SCN1179140", "Lucrări de reparații interioare la Locuința Protejată „Sfântul Pavel”", null,
                ["45453000-7"], 20_661m, "RON",
                "Directia Generala de Asistenta Sociala si Protectia Copilului Brasov",
                now.AddDays(-2), now.AddDays(20), [], County: "Brașov")],
            new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));

        SeapIngestionService service = new(db, api, new StubAttachmentStorage(), new NullSeapCategorizer(), new NullSeapSummaryGenerator(),
            Options.Create(new SeapOptions()), new FakeTimeProvider(), NullLogger<SeapIngestionService>.Instance);

        IReadOnlyCollection<SeapTender> imported = await service.SearchAndImportAsync(
            ["45453000"], ["Brașov"], new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), CancellationToken.None);

        Assert.Contains(imported, tender => tender.SeapExternalId == "SCN1179140");
        Assert.Equal("Brașov", (await db.SeapTenders.SingleAsync(tender => tender.SeapExternalId == "SCN1179140")).County);
        Assert.Contains((today.AddDays(-30), today), api.Windows);
    }

    [Fact]
    public async Task SearchImportedTenderPersistsTheNoticeSourceForOnDemandDocumentResolution()
    {
        DbContextOptions<MarketplaceDbContext> options = TestDatabase.CreateOptions();
        await using MarketplaceDbContext db = new(options);
        await db.Database.MigrateAsync();

        DateTimeOffset now = DateTimeOffset.UtcNow;
        // The search payload carries the numeric noticeId/type; the notice-detail API resolves the docs.
        SeapTenderNoticeDto notice = new("CN-DOCS-1", "Caiet de sarcini sala de sport", null, ["45214200-2"], 2_000_000m, "RON",
            "PRIMARIA GALATI", now, now.AddDays(30), [], County: "Galați", NoticeId: 900001, SysNoticeTypeId: 17);
        DocumentedSeapApiClient api = new([notice],
        [
            new SeapNoticeDocumentDto("CN-DOCS-1/00005", "4. Caiet de sarcini sala sport.pdf.p7s", "https://e-licitatie.ro/api-pub/files/noticedoc/aaa"),
            new SeapNoticeDocumentDto("CN-DOCS-1/00009", "FisaDate.pdf", "https://e-licitatie.ro/api-pub/files/noticedoc/bbb")
        ]);
        SeapIngestionService service = new(db, api, new StubAttachmentStorage(), new NullSeapCategorizer(), new NullSeapSummaryGenerator(),
            Options.Create(new SeapOptions()), new FakeTimeProvider(), NullLogger<SeapIngestionService>.Instance);

        IReadOnlyCollection<SeapTender> imported = await service.SearchAndImportAsync(
            ["45210000"], ["Galați"], new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), CancellationToken.None);

        SeapTender tender = Assert.Single(imported);
        // Documents are resolved on demand (POST /tenders/{id}/documents): a bulk search never pays one
        // notice-detail HTTP call per match, so it can import hundreds of tenders quickly and the discovery
        // tab can reflect the real SEAP universe (ADR-012 on-demand resolution).
        Assert.Empty(tender.Attachments);
        Assert.Equal(0, api.DocumentFetches);
        // The persisted numeric notice id/type is what lets the „Documente Originale" tab resolve the docs.
        Assert.Equal(900001, tender.NoticeId);
        Assert.Equal(17, tender.SysNoticeTypeId);
    }

    [Fact]
    public async Task SearchSkipsNoticesWhoseSubmissionDeadlineAlreadyPassed()
    {
        DbContextOptions<MarketplaceDbContext> options = TestDatabase.CreateOptions();
        await using MarketplaceDbContext db = new(options);
        await db.Database.MigrateAsync();

        DateTimeOffset now = DateTimeOffset.UtcNow;
        StubSeapApiClient api = new(
            [
                new SeapTenderNoticeDto("CN-EXPIRED-1", "Anunț cu termen depășit", null, ["45214200-2"], 1000m, "RON",
                    "PRIMARIA GALATI", now.AddDays(-60), now.AddDays(-30), [], County: "Galați"),
                new SeapTenderNoticeDto("CN-OPEN-1", "Anunț în termen", null, ["45214200-2"], 1000m, "RON",
                    "PRIMARIA GALATI", now, now.AddDays(30), [], County: "Galați")
            ],
            []);

        SeapIngestionService service = new(db, api, new StubAttachmentStorage(), new NullSeapCategorizer(), new NullSeapSummaryGenerator(),
            Options.Create(new SeapOptions()), new FakeTimeProvider(), NullLogger<SeapIngestionService>.Instance);

        IReadOnlyCollection<SeapTender> imported = await service.SearchAndImportAsync(
            ["45210000"], ["Galați"], null, null, CancellationToken.None);

        // Expired notices are never bid targets: only the notice whose deadline is still in the future
        // enters the local cache, even though the live search returned both.
        Assert.Equal(["CN-OPEN-1"], imported.Select(tender => tender.SeapExternalId).ToArray());
        Assert.Equal(1, await db.SeapTenders.CountAsync());
    }

    private sealed class StubSeapApiClient(
        IReadOnlyCollection<SeapTenderNoticeDto> page0,
        IReadOnlyCollection<SeapTenderNoticeDto> page1) : ISeapApiClient
    {
        public List<(DateOnly? From, DateOnly? To)> Windows { get; } = [];

        public Task<IReadOnlyCollection<SeapTenderNoticeDto>> FetchRecentNoticesAsync(CancellationToken cancellationToken) =>
            Task.FromResult(page0);

        public Task<IReadOnlyCollection<SeapTenderNoticeDto>> FetchNoticePageAsync(
            int pageIndex, int pageSize, DateOnly? startPublicationDate, DateOnly? endPublicationDate,
            CancellationToken cancellationToken)
        {
            Windows.Add((startPublicationDate, endPublicationDate));
            return Task.FromResult<IReadOnlyCollection<SeapTenderNoticeDto>>(
                pageIndex >= 2 ? Array.Empty<SeapTenderNoticeDto>() : pageIndex == 0 ? page0 : page1);
        }

        public Task<Stream> DownloadAttachmentAsync(string downloadUrl, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Attachments are disabled in this test.");
        public Task<IReadOnlyCollection<SeapNoticeDocumentDto>> FetchNoticeDocumentsAsync(
            int noticeId, int sysNoticeTypeId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<SeapNoticeDocumentDto>>([]);
    }

    /// <summary>Returns nothing for the requested window but the recent notice for every other window —
    /// simulating the platform's capped/rotating wide slice.</summary>
    private sealed class StubRecentWindowApiClient(
        IReadOnlyCollection<SeapTenderNoticeDto> recent,
        DateOnly primaryFrom,
        DateOnly primaryTo) : ISeapApiClient
    {
        public List<(DateOnly? From, DateOnly? To)> Windows { get; } = [];

        public Task<IReadOnlyCollection<SeapTenderNoticeDto>> FetchRecentNoticesAsync(CancellationToken cancellationToken) =>
            Task.FromResult(recent);

        public Task<IReadOnlyCollection<SeapTenderNoticeDto>> FetchNoticePageAsync(
            int pageIndex, int pageSize, DateOnly? startPublicationDate, DateOnly? endPublicationDate,
            CancellationToken cancellationToken)
        {
            Windows.Add((startPublicationDate, endPublicationDate));
            bool isPrimaryWindow = startPublicationDate == primaryFrom && endPublicationDate == primaryTo;
            return Task.FromResult<IReadOnlyCollection<SeapTenderNoticeDto>>(
                isPrimaryWindow ? Array.Empty<SeapTenderNoticeDto>() : pageIndex == 0 ? recent : Array.Empty<SeapTenderNoticeDto>());
        }

        public Task<Stream> DownloadAttachmentAsync(string downloadUrl, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Attachments are disabled in this test.");
        public Task<IReadOnlyCollection<SeapNoticeDocumentDto>> FetchNoticeDocumentsAsync(
            int noticeId, int sysNoticeTypeId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<SeapNoticeDocumentDto>>([]);
    }

    /// <summary>Search client that returns the published documents for the matched notice — simulating the
    /// notice-detail API resolving the document list the search payload does not carry.</summary>
    private sealed class DocumentedSeapApiClient(
        IReadOnlyCollection<SeapTenderNoticeDto> page0,
        IReadOnlyCollection<SeapNoticeDocumentDto> documents) : ISeapApiClient
    {
        public int DocumentFetches { get; private set; }

        public Task<IReadOnlyCollection<SeapTenderNoticeDto>> FetchRecentNoticesAsync(CancellationToken cancellationToken) =>
            Task.FromResult(page0);

        public Task<IReadOnlyCollection<SeapTenderNoticeDto>> FetchNoticePageAsync(
            int pageIndex, int pageSize, DateOnly? startPublicationDate, DateOnly? endPublicationDate,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<SeapTenderNoticeDto>>(
                pageIndex >= 1 ? Array.Empty<SeapTenderNoticeDto>() : page0);

        public Task<Stream> DownloadAttachmentAsync(string downloadUrl, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Attachments are disabled in this test.");

        public Task<IReadOnlyCollection<SeapNoticeDocumentDto>> FetchNoticeDocumentsAsync(
            int noticeId, int sysNoticeTypeId, CancellationToken cancellationToken)
        {
            DocumentFetches++;
            return Task.FromResult(documents);
        }
    }

    private sealed class NullSeapCategorizer : ISeapCategorizer
    {
        public Task<SeapCategorizationResult?> CategorizeAsync(string? cpvCode, string? cpvName, string title, CancellationToken cancellationToken) =>
            Task.FromResult<SeapCategorizationResult?>(null);
    }

    private sealed class NullSeapSummaryGenerator : ISeapSummaryGenerator
    {
        public Task<SeapAiExtraction?> GenerateAsync(SeapTender tender, string? attachmentText, CancellationToken cancellationToken) =>
            Task.FromResult<SeapAiExtraction?>(null);
    }

    private sealed class StubAttachmentStorage : ISeapAttachmentStorage
    {
        public Task<string> UploadAsync(string tenderId, string fileName, Stream content, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Attachments are disabled in this test.");
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UtcNow;
    }
}
