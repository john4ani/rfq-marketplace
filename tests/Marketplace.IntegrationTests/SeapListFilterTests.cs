using Marketplace.Application.Seap;
using Marketplace.Domain.Seap;
using Marketplace.Infrastructure.Bidding;
using Marketplace.Infrastructure.Companies;
using Marketplace.Infrastructure.Documents;
using Marketplace.Infrastructure.Embeddings;
using Marketplace.Infrastructure.Persistence;
using Marketplace.Infrastructure.Seap;
using Marketplace.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Marketplace.IntegrationTests;

// Seap list filtering: the AI-mapped CategoryId is authoritative, the CPV-prefix match is the fallback
// for uncategorized tenders, and the radius filter measures county-seat distances (tenders without a
// recognizable county are excluded only while the radius filter is active).
[Collection(IntegrationTestGroup.Name)]
public sealed class SeapListFilterTests : IntegrationTestBase
{
    [Fact]
    public async Task CategoryFilterUsesAiCategoryAndFallsBackToCpvPrefix()
    {
        DbContextOptions<MarketplaceDbContext> options = TestDatabase.CreateOptions();
        await using MarketplaceDbContext db = new(options);
        await db.Database.MigrateAsync();

        Guid constructiiId = await db.Categories.Where(category => category.Slug == "constructii").Select(category => category.Id).SingleAsync();
        Guid transportId = await db.Categories.Where(category => category.Slug == "transport-logistica").Select(category => category.Id).SingleAsync();
        await SeedTendersAsync(db, constructiiId);

        BiddingGenerationService service = NewService(db);

        // Categorized tender (CategoryId authoritative) + uncategorized transport tender (prefix fallback).
        IReadOnlyCollection<SeapTender> constructionOnly = await service.ListTendersAsync([constructiiId], null, null, CancellationToken.None);
        Assert.Equal(["CN-CAT-CL", "CN-CAT-VS"], constructionOnly.Select(item => item.SeapExternalId).OrderBy(id => id).ToArray());

        IReadOnlyCollection<SeapTender> transportOnly = await service.ListTendersAsync([transportId], null, null, CancellationToken.None);
        Assert.Equal(["CN-UNC-TR"], transportOnly.Select(item => item.SeapExternalId).ToArray());

        // No selection = everything.
        Assert.Equal(4, (await service.ListTendersAsync(null, null, null, CancellationToken.None)).Count);
    }

    [Fact]
    public async Task RadiusFilterExcludesFarAndUnknownCounties()
    {
        DbContextOptions<MarketplaceDbContext> options = TestDatabase.CreateOptions();
        await using MarketplaceDbContext db = new(options);
        await db.Database.MigrateAsync();

        Guid constructiiId = await db.Categories.Where(category => category.Slug == "constructii").Select(category => category.Id).SingleAsync();
        await SeedTendersAsync(db, constructiiId);

        BiddingGenerationService service = NewService(db);

        // 50 km from Cluj keeps the Cluj tender only; Vaslui (~300 km) and unknown counties are excluded.
        IReadOnlyCollection<SeapTender> nearCluj = await service.ListTendersAsync(null, 50, "Cluj", CancellationToken.None);
        Assert.Equal(["CN-CAT-CL"], nearCluj.Select(item => item.SeapExternalId).ToArray());

        // Radius + category combine (AND).
        IReadOnlyCollection<SeapTender> constructionNearCluj = await service.ListTendersAsync([constructiiId], 50, "Cluj", CancellationToken.None);
        Assert.Equal(["CN-CAT-CL"], constructionNearCluj.Select(item => item.SeapExternalId).ToArray());

        // A wide radius still respects county-seat geometry and keeps excluding unknown counties:
        // Iași (~306 km) and Vaslui (~316 km) are both inside 400 km from Cluj, the unknown-county tender is not.
        IReadOnlyCollection<SeapTender> wide = await service.ListTendersAsync(null, 400, "Cluj", CancellationToken.None);
        Assert.Equal(["CN-CAT-CL", "CN-CAT-VS", "CN-UNC-TR"], wide.Select(item => item.SeapExternalId).OrderBy(id => id).ToArray());
    }

    [Fact]
    public async Task ProfileCpvPrefixAndCountyFiltersAreDeterministic()
    {
        DbContextOptions<MarketplaceDbContext> options = TestDatabase.CreateOptions();
        await using MarketplaceDbContext db = new(options);
        await db.Database.MigrateAsync();

        Guid constructiiId = await db.Categories.Where(category => category.Slug == "constructii").Select(category => category.Id).SingleAsync();
        await SeedTendersAsync(db, constructiiId);

        BiddingGenerationService service = NewService(db);

        // CPV prefixes match categorized AND uncategorized tenders (they are not limited to the
        // category-prefix fallback), so "452" keeps the two construction tenders only.
        IReadOnlyCollection<SeapTender> byCpv = await service.ListTendersAsync(null, null, null, CancellationToken.None, ["452"], null);
        Assert.Equal(["CN-CAT-CL", "CN-CAT-VS"], byCpv.Select(item => item.SeapExternalId).OrderBy(id => id).ToArray());

        // County codes and names both normalize to the canonical county the tender stores.
        IReadOnlyCollection<SeapTender> byCountyCode = await service.ListTendersAsync(null, null, null, CancellationToken.None, null, ["IS"]);
        Assert.Equal(["CN-UNC-TR"], byCountyCode.Select(item => item.SeapExternalId).ToArray());
        IReadOnlyCollection<SeapTender> byCountyName = await service.ListTendersAsync(null, null, null, CancellationToken.None, null, ["Iasi"]);
        Assert.Equal(["CN-UNC-TR"], byCountyName.Select(item => item.SeapExternalId).ToArray());

        // Both profile filters compose with each other.
        IReadOnlyCollection<SeapTender> combined = await service.ListTendersAsync(null, null, null, CancellationToken.None, ["601"], ["IS"]);
        Assert.Equal(["CN-UNC-TR"], combined.Select(item => item.SeapExternalId).ToArray());

        // Unknown counties/prefixes exclude everything; a county that is not a county seat is excluded.
        Assert.Empty(await service.ListTendersAsync(null, null, null, CancellationToken.None, null, ["Maramureș"]));
        Assert.Empty(await service.ListTendersAsync(null, null, null, CancellationToken.None, ["99"], null));
        Assert.Empty(await service.ListTendersAsync(null, null, null, CancellationToken.None, null, ["Hunedoara"]));
    }

    [Fact]
    public async Task GroupCpvPrefixesMatchGranularTenderCodes()
    {
        DbContextOptions<MarketplaceDbContext> options = TestDatabase.CreateOptions();
        await using MarketplaceDbContext db = new(options);
        await db.Database.MigrateAsync();

        DateTimeOffset now = DateTimeOffset.UtcNow;
        db.SeapTenders.AddRange(
            SeapTenderMapper.MapNotice(
                new SeapTenderNoticeDto("CN-GRP-CL", "Construcții clădiri Cluj", null, ["45215120-4"], 1000m, "RON",
                    "MUNICIPIUL CLUJ-NAPOCA", now, now.AddDays(30), [], County: "Cluj"), now),
            SeapTenderMapper.MapNotice(
                new SeapTenderNoticeDto("CN-GRP-PD", "Pod peste râu", null, ["45221111-2"], 2000m, "RON",
                    "CONSILIUL JUDETEAN ALBA", now, now.AddDays(30), [], County: "Alba"), now),
            SeapTenderMapper.MapNotice(
                new SeapTenderNoticeDto("CN-GRP-MD", "Medicamente", null, ["33652100-6"], 500m, "RON",
                    "SPITAL MURES", now, now.AddDays(30), [], County: "Mureș"), now));
        await db.SaveChangesAsync();

        BiddingGenerationService service = NewService(db);

        // The 8-digit buildings-class prefix "45210000" matches the granular 45215120-4 code through the
        // 4521 group (it used to be an exact-prefix miss), but not the 4522 bridge nor the 3365 medicine.
        IReadOnlyCollection<SeapTender> buildings = await service.ListTendersAsync(null, null, null, CancellationToken.None, ["45210000"], null);
        Assert.Equal(["CN-GRP-CL"], buildings.Select(item => item.SeapExternalId).ToArray());

        // An exact granular code stays an exact prefix match.
        IReadOnlyCollection<SeapTender> bridges = await service.ListTendersAsync(null, null, null, CancellationToken.None, ["45221111"], null);
        Assert.Equal(["CN-GRP-PD"], bridges.Select(item => item.SeapExternalId).ToArray());

        // A division reference like the food "15000000" matches nothing among these tenders.
        Assert.Empty(await service.ListTendersAsync(null, null, null, CancellationToken.None, ["15000000"], null));
    }

    [Fact]
    public async Task ClosedTendersAreExcludedFromTheDiscoveryList()
    {
        DbContextOptions<MarketplaceDbContext> options = TestDatabase.CreateOptions();
        await using MarketplaceDbContext db = new(options);
        await db.Database.MigrateAsync();

        DateTimeOffset now = DateTimeOffset.UtcNow;
        SeapTender open = SeapTenderMapper.MapNotice(
            new SeapTenderNoticeDto("CN-OPEN-1", "Anunț deschis", null, ["45200000-1"], 1000m, "RON",
                "PRIMARIA CLUJ", now, now.AddDays(30), []), now);
        SeapTender closed = SeapTenderMapper.MapNotice(
            new SeapTenderNoticeDto("CN-CLOSED-1", "Anunț închis", null, ["45200000-1"], 1000m, "RON",
                "PRIMARIA CLUJ", now.AddDays(-60), now.AddDays(-30), []), now);
        db.SeapTenders.AddRange(open, closed);
        await db.SaveChangesAsync();

        BiddingGenerationService service = NewService(db);

        IReadOnlyCollection<SeapTender> tenders = await service.ListTendersAsync(null, null, null, CancellationToken.None);

        // The discovery feed shows only procedurally open tenders — closed procedures are not bid targets.
        Assert.Equal(["CN-OPEN-1"], tenders.Select(item => item.SeapExternalId).ToArray());
    }

    [Fact]
    public async Task GeneratedOffersTabIsTheCompanyHistoryAndKeepsClosedTenders()
    {
        DbContextOptions<MarketplaceDbContext> options = TestDatabase.CreateOptions();
        await using MarketplaceDbContext db = new(options);
        await db.Database.MigrateAsync();

        DateTimeOffset now = DateTimeOffset.UtcNow;
        SeapTender openWithBid = SeapTenderMapper.MapNotice(
            new SeapTenderNoticeDto("CN-BID-OPEN", "Anunț deschis cu ofertă", null, ["45200000-1"], 1000m, "RON",
                "PRIMARIA CLUJ", now, now.AddDays(30), []), now);
        openWithBid.RecordGeneratedBid("Ofertă tehnică", "local://p.docx", "local://b.xlsx", now);

        SeapTender closedWithBid = SeapTenderMapper.MapNotice(
            new SeapTenderNoticeDto("CN-BID-CLOSED", "Anunț închis cu ofertă", null, ["45200000-1"], 1000m, "RON",
                "PRIMARIA CLUJ", now.AddDays(-60), now.AddDays(-30), []), now);
        closedWithBid.RecordGeneratedBid("Ofertă tehnică veche", "local://p2.docx", "local://b2.xlsx", now);

        SeapTender openReady = SeapTenderMapper.MapNotice(
            new SeapTenderNoticeDto("CN-READY-OPEN", "Anunț deschis fără ofertă", null, ["45200000-1"], 1000m, "RON",
                "PRIMARIA CLUJ", now, now.AddDays(30), []), now);
        openReady.MarkReadyForBidding(now);

        db.SeapTenders.AddRange(openWithBid, closedWithBid, openReady);
        await db.SaveChangesAsync();

        BiddingGenerationService service = NewService(db);

        // Tab 1 „Caută anunțuri SEAP": only ready-to-bid, procedurally open tenders — the closed one is out.
        IReadOnlyCollection<SeapTender> discovery = await service.ListTendersAsync(
            null, null, null, CancellationToken.None, status: SeapTenderStatus.ReadyForBidding);
        Assert.Equal(["CN-READY-OPEN"], discovery.Select(item => item.SeapExternalId).ToArray());

        // Tab 2 „Oferte Generate": the company's activity history — generated offers stay reachable after
        // the tender's deadline passed.
        IReadOnlyCollection<SeapTender> history = await service.ListTendersAsync(
            null, null, null, CancellationToken.None, status: SeapTenderStatus.BidGenerated);
        Assert.Equal(["CN-BID-CLOSED", "CN-BID-OPEN"], history.Select(item => item.SeapExternalId).OrderBy(id => id).ToArray());

        // Without a status filter the discovery feed also excludes closed tenders (existing behavior).
        IReadOnlyCollection<SeapTender> allOpen = await service.ListTendersAsync(null, null, null, CancellationToken.None);
        Assert.Equal(["CN-BID-OPEN", "CN-READY-OPEN"], allOpen.Select(item => item.SeapExternalId).OrderBy(id => id).ToArray());
    }

    private static async Task SeedTendersAsync(MarketplaceDbContext db, Guid constructiiId)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        SeapTender cluj = SeapTenderMapper.MapNotice(
            new SeapTenderNoticeDto("CN-CAT-CL", "Lucrări de construcții Cluj", null, ["45200000-1"], 1000m, "RON",
                "PRIMARIA MUNICIPIULUI CLUJ-NAPOCA", now, now.AddDays(30), [], County: "Cluj"), now);
        cluj.Categorize(constructiiId, now);

        SeapTender vaslui = SeapTenderMapper.MapNotice(
            new SeapTenderNoticeDto("CN-CAT-VS", "Lucrări de construcții Vaslui", null, ["45200000-1"], 900m, "RON",
                "17095927 - DIRECTIA GENERALA DE ASISTENTA SOCIALA SI PROTECTIA COPILULUI VASLUI", now, now.AddDays(30), [], County: "Vaslui"), now);
        vaslui.Categorize(constructiiId, now);

        // Un-categorized transport tender — only reachable through the CPV-prefix fallback.
        SeapTender transport = SeapTenderMapper.MapNotice(
            new SeapTenderNoticeDto("CN-UNC-TR", "Transport mărfuri Iași", null, ["60100000-9"], 500m, "RON",
                "PRIMARIA MUNICIPIULUI IASI", now, now.AddDays(30), [], County: "Iași"), now);

        // Un-categorized tender whose county is not recognizable — excluded when a radius filter is active.
        SeapTender noCounty = SeapTenderMapper.MapNotice(
            new SeapTenderNoticeDto("CN-UNC-MD", "Medicamente", null, ["33652100-6"], 200m, "RON",
                "SPITALUL DE URGENTA PETROSANI", now, now.AddDays(30), []), now);

        db.SeapTenders.AddRange(cluj, vaslui, transport, noCounty);
        await db.SaveChangesAsync();
    }

    private static BiddingGenerationService NewService(MarketplaceDbContext db) => new(
        db,
        new StaticMockLlmProvider(StaticMockLlmProvider.RomanianProcurementExpertProposal),
        new CompanyQualificationService(db, TimeProvider.System),
        new DocumentGenerator(),
        new UnavailableEmbeddingService(),
        Options.Create(new AutoBiddingOptions { IsShadowMode = true }),
        Options.Create(new LocalStorageOptions { UseLocalStorage = false }),
        new MemoryStorageService(),
        TimeProvider.System,
        NullLogger<BiddingGenerationService>.Instance);

    private sealed class UnavailableEmbeddingService : IEmbeddingService
    {
        public Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Embeddings neconfigurate.");
    }

    private sealed class MemoryStorageService : IStorageService
    {
        public Task<string> UploadAsync(string bucket, string key, string fileName, Stream content, string contentType, CancellationToken cancellationToken) =>
            Task.FromResult($"memory://{key}/{fileName}");
        public Task<StorageObject?> OpenReadAsync(string reference, CancellationToken cancellationToken) =>
            Task.FromResult<StorageObject?>(null);
    }
}
