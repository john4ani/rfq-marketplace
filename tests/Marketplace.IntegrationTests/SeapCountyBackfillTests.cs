using Marketplace.Application.Seap;
using Marketplace.Domain.Seap;
using Marketplace.Infrastructure.Persistence;
using Marketplace.Infrastructure.Seap;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Marketplace.IntegrationTests;

// Deterministic county backfill for tenders imported before the county enrichment existed (e.g. rows
// created before the AddSeapEnrichmentFields migration): the county is re-derived from the
// contracting-authority name via CountyParser — no AI, idempotent, unrecognizable names stay null.
[Collection(IntegrationTestGroup.Name)]
public sealed class SeapCountyBackfillTests : IntegrationTestBase
{
    [Fact]
    public async Task BackfillDerivesCountyForPreviouslyImportedTendersWithoutOne()
    {
        DbContextOptions<MarketplaceDbContext> options = TestDatabase.CreateOptions();
        await using MarketplaceDbContext db = new(options);
        await db.Database.MigrateAsync();

        DateTimeOffset now = DateTimeOffset.UtcNow;
        SeapIngestionService service = new(db, new StubSeapApiClient([]), new StubAttachmentStorage(),
            new NullSeapCategorizer(), new NullSeapSummaryGenerator(), Options.Create(new SeapOptions()), new FakeTimeProvider(),
            NullLogger<SeapIngestionService>.Instance);

        // Import tenders whose notices carry no county — exactly what pre-enrichment imports look like.
        await service.ImportNoticeAsync(Notice("CN-BF-1", "4305857 - Municipiul Cluj-Napoca", now), CancellationToken.None);
        await service.ImportNoticeAsync(Notice("CN-BF-2", "RO 7796899 - REGIA AUTONOMA JUDETEANA DRUMURI SI PODURI VALCEA RA", now), CancellationToken.None);
        await service.ImportNoticeAsync(Notice("CN-BF-3", "4374873 - SPITALUL DE URGENTA PETROSANI", now), CancellationToken.None);
        Assert.All(await db.SeapTenders.ToListAsync(), tender => Assert.Null(tender.County));

        int updated = await service.BackfillCountiesAsync(CancellationToken.None);

        Assert.Equal(2, updated);
        Assert.Equal("Cluj", (await db.SeapTenders.SingleAsync(tender => tender.SeapExternalId == "CN-BF-1")).County);
        Assert.Equal("Vâlcea", (await db.SeapTenders.SingleAsync(tender => tender.SeapExternalId == "CN-BF-2")).County);
        // Petroșani is not a county seat → stays null, and a second pass is a no-op.
        Assert.Null((await db.SeapTenders.SingleAsync(tender => tender.SeapExternalId == "CN-BF-3")).County);
        Assert.Equal(0, await service.BackfillCountiesAsync(CancellationToken.None));
    }

    private static SeapTenderNoticeDto Notice(string id, string buyerName, DateTimeOffset now) =>
        new(id, "Anunț de test", null, ["45233000-1"], 1000m, "RON", buyerName, now, now.AddDays(30), []);

    private sealed class StubSeapApiClient(IReadOnlyCollection<SeapTenderNoticeDto> notices) : ISeapApiClient
    {
        public Task<IReadOnlyCollection<SeapTenderNoticeDto>> FetchRecentNoticesAsync(CancellationToken cancellationToken) =>
            Task.FromResult(notices);
        public Task<IReadOnlyCollection<SeapTenderNoticeDto>> FetchNoticePageAsync(
            int pageIndex, int pageSize, DateOnly? startPublicationDate, DateOnly? endPublicationDate,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<SeapTenderNoticeDto>>(Array.Empty<SeapTenderNoticeDto>());
        public Task<Stream> DownloadAttachmentAsync(string downloadUrl, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Attachments are disabled in this test.");
        public Task<IReadOnlyCollection<SeapNoticeDocumentDto>> FetchNoticeDocumentsAsync(
            int noticeId, int sysNoticeTypeId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<SeapNoticeDocumentDto>>([]);
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
