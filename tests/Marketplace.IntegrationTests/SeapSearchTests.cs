using Marketplace.Application.Seap;
using Marketplace.Domain.Seap;
using Marketplace.Infrastructure.Persistence;
using Marketplace.Infrastructure.Seap;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Testcontainers.PostgreSql;

namespace Marketplace.IntegrationTests;

// On-demand „Caută în SEAP": the search takes the active CPV/county parameters, pages the live SEAP
// search (honoring the publication-date window), derives each notice's county deterministically, imports
// only the matching notices (no LLM, no attachments), and returns them — bypassing the background
// hourly import cap because it is a user-initiated action.
public sealed class SeapSearchTests
{
    [Fact]
    public async Task SearchAndImportTakesParametersAndAddsMatchingNoticesFromThePlatform()
    {
        await using PostgreSqlContainer postgres = new PostgreSqlBuilder("pgvector/pgvector:pg17")
            .WithDatabase("marketplace_seap_search_test")
            .Build();
        await postgres.StartAsync();

        DbContextOptions<MarketplaceDbContext> options = new DbContextOptionsBuilder<MarketplaceDbContext>()
            .UseNpgsql(postgres.GetConnectionString(), npgsql =>
            {
                npgsql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(5), null);
                npgsql.MigrationsAssembly(typeof(MarketplaceDbContext).Assembly.FullName);
                npgsql.UseVector();
            })
            .Options;
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

        SeapIngestionService service = new(db, api, new StubAttachmentStorage(), new NullSeapCategorizer(),
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

        // The publication-date window was forwarded to the platform client.
        Assert.Equal(new DateOnly(2026, 1, 1), api.LastFrom);
        Assert.Equal(new DateOnly(2026, 12, 31), api.LastTo);

        // A second search for the same parameters skips duplicates (already imported).
        IReadOnlyCollection<SeapTender> second = await service.SearchAndImportAsync(
            ["45210000"], ["Galați"], null, null, CancellationToken.None);
        Assert.Empty(second);
        Assert.Equal(2, await db.SeapTenders.CountAsync());
    }

    private sealed class StubSeapApiClient(
        IReadOnlyCollection<SeapTenderNoticeDto> page0,
        IReadOnlyCollection<SeapTenderNoticeDto> page1) : ISeapApiClient
    {
        public DateOnly? LastFrom { get; private set; }
        public DateOnly? LastTo { get; private set; }

        public Task<IReadOnlyCollection<SeapTenderNoticeDto>> FetchRecentNoticesAsync(CancellationToken cancellationToken) =>
            Task.FromResult(page0);

        public Task<IReadOnlyCollection<SeapTenderNoticeDto>> FetchNoticePageAsync(
            int pageIndex, int pageSize, DateOnly? startPublicationDate, DateOnly? endPublicationDate,
            CancellationToken cancellationToken)
        {
            LastFrom = startPublicationDate;
            LastTo = endPublicationDate;
            return Task.FromResult<IReadOnlyCollection<SeapTenderNoticeDto>>(
                pageIndex >= 2 ? Array.Empty<SeapTenderNoticeDto>() : pageIndex == 0 ? page0 : page1);
        }

        public Task<Stream> DownloadAttachmentAsync(string downloadUrl, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Attachments are disabled in this test.");
    }

    private sealed class NullSeapCategorizer : ISeapCategorizer
    {
        public Task<SeapCategorizationResult?> CategorizeAsync(string? cpvCode, string? cpvName, string title, CancellationToken cancellationToken) =>
            Task.FromResult<SeapCategorizationResult?>(null);
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
