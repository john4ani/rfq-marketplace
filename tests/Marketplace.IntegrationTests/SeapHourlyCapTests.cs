using Marketplace.Application.Seap;
using Marketplace.Domain.Seap;
using Marketplace.Infrastructure.Persistence;
using Marketplace.Infrastructure.Seap;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Testcontainers.PostgreSql;

namespace Marketplace.IntegrationTests;

/// <summary>
/// Guards the SEAP hourly import cap (cost guardrail): at most <c>Seap:MaxNewNoticesPerHour</c> new
/// tenders are imported per rolling hour, even when the public API returns more notices. Runs on a
/// dedicated disposable pgvector container so it cannot interfere with the shared integration database.
/// </summary>
public sealed class SeapHourlyCapTests
{
    private const int HourlyCap = 5;
    private static readonly string[] TargetCpv = ["33652100-6"];

    [Fact]
    public async Task PollAndImportNeverExceedsTheHourlyCap()
    {
        await using PostgreSqlContainer postgres = new PostgreSqlBuilder("pgvector/pgvector:pg17")
            .WithDatabase("marketplace_hourly_cap_test")
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

        List<SeapTenderNoticeDto> notices = Enumerable.Range(1, 12)
            .Select(index => new SeapTenderNoticeDto(
                $"CN-CAP-TEST-{index}",
                $"Anunț cap orar {index}",
                null,
                TargetCpv,
                1000m + index,
                "RON",
                "Entitate Test",
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddDays(30),
                Array.Empty<SeapAttachmentDto>()))
            .ToList();

        StubSeapApiClient api = new(notices);
        StubAttachmentStorage storage = new();
        SeapOptions seapOptions = new() { MaxNewNoticesPerHour = HourlyCap };
        FakeTimeProvider clock = new();

        SeapIngestionService service = new(db, api, storage, new NullSeapCategorizer(), new NullSeapSummaryGenerator(),
            Options.Create(seapOptions), clock, NullLogger<SeapIngestionService>.Instance);

        // First poll: the API returns 12 notices but only the configured cap may be imported.
        int firstPoll = await service.PollAndImportAsync(CancellationToken.None);
        Assert.Equal(HourlyCap, firstPoll);
        Assert.Equal(HourlyCap, await db.SeapTenders.CountAsync());

        // Second poll within the same rolling hour: cap already reached -> no further imports at all.
        int secondPoll = await service.PollAndImportAsync(CancellationToken.None);
        Assert.Equal(0, secondPoll);
        Assert.Equal(HourlyCap, await db.SeapTenders.CountAsync());

        // One hour and a bit later a fresh window opens; duplicates are skipped and the next batch is imported.
        clock.Now = clock.Now.AddHours(1).AddMinutes(1);
        int thirdPoll = await service.PollAndImportAsync(CancellationToken.None);
        Assert.Equal(HourlyCap, thirdPoll);
        Assert.Equal(HourlyCap * 2, await db.SeapTenders.CountAsync());
    }

    [Fact]
    public async Task RealPayloadWithNonUtcOffsetsPersistsAsUtc()
    {
        await using PostgreSqlContainer postgres = new PostgreSqlBuilder("pgvector/pgvector:pg17")
            .WithDatabase("marketplace_seap_utc_test")
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

        // The live e-licitatie.ro fixture carries Romania's local offset (+03:00); Npgsql requires
        // UTC (offset 0) when persisting `timestamp with time zone`.
        string json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "seap-notices-full.json"));
        SeapTenderNoticeDto notice = SeapNoticeParser.ParseNotices(json).First();
        Assert.NotEqual(TimeSpan.Zero, notice.PublicationDate.Offset);

        SeapOptions seapOptions = new() { MaxNewNoticesPerHour = HourlyCap };
        SeapIngestionService service = new(
            db, new StubSeapApiClient([]), new StubAttachmentStorage(), new NullSeapCategorizer(), new NullSeapSummaryGenerator(),
            Options.Create(seapOptions), new FakeTimeProvider(), NullLogger<SeapIngestionService>.Instance);

        SeapTenderImportResult result = await service.ImportNoticeAsync(notice, CancellationToken.None);

        Assert.NotNull(result.Tender);
        SeapTender saved = await db.SeapTenders.SingleAsync(tender => tender.SeapExternalId == notice.Id);
        Assert.Equal(TimeSpan.Zero, saved.PublicationDate.Offset);
        Assert.Equal(notice.PublicationDate.ToUniversalTime(), saved.PublicationDate);
        Assert.Equal(notice.Deadline.ToUniversalTime(), saved.Deadline);
    }

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
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
