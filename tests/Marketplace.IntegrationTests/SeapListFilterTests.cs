using Marketplace.Application.Seap;
using Marketplace.Domain.Seap;
using Marketplace.Infrastructure.Bidding;
using Marketplace.Infrastructure.Documents;
using Marketplace.Infrastructure.Embeddings;
using Marketplace.Infrastructure.Persistence;
using Marketplace.Infrastructure.Seap;
using Marketplace.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Testcontainers.PostgreSql;

namespace Marketplace.IntegrationTests;

// Seap list filtering: the AI-mapped CategoryId is authoritative, the CPV-prefix match is the fallback
// for uncategorized tenders, and the radius filter measures county-seat distances (tenders without a
// recognizable county are excluded only while the radius filter is active).
public sealed class SeapListFilterTests
{
    [Fact]
    public async Task CategoryFilterUsesAiCategoryAndFallsBackToCpvPrefix()
    {
        await using PostgreSqlContainer postgres = new PostgreSqlBuilder("pgvector/pgvector:pg17")
            .WithDatabase("marketplace_seap_list_filter_test")
            .Build();
        await postgres.StartAsync();

        DbContextOptions<MarketplaceDbContext> options = NewOptions(postgres);
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
        await using PostgreSqlContainer postgres = new PostgreSqlBuilder("pgvector/pgvector:pg17")
            .WithDatabase("marketplace_seap_radius_filter_test")
            .Build();
        await postgres.StartAsync();

        DbContextOptions<MarketplaceDbContext> options = NewOptions(postgres);
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
        await using PostgreSqlContainer postgres = new PostgreSqlBuilder("pgvector/pgvector:pg17")
            .WithDatabase("marketplace_seap_profile_filter_test")
            .Build();
        await postgres.StartAsync();

        DbContextOptions<MarketplaceDbContext> options = NewOptions(postgres);
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
        new DocumentGenerator(),
        new UnavailableEmbeddingService(),
        Options.Create(new AutoBiddingOptions { IsShadowMode = true }),
        Options.Create(new LocalStorageOptions { UseLocalStorage = false }),
        new MemoryStorageService(),
        TimeProvider.System,
        NullLogger<BiddingGenerationService>.Instance);

    private static DbContextOptions<MarketplaceDbContext> NewOptions(PostgreSqlContainer postgres) =>
        new DbContextOptionsBuilder<MarketplaceDbContext>()
            .UseNpgsql(postgres.GetConnectionString(), npgsql =>
            {
                npgsql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(5), null);
                npgsql.MigrationsAssembly(typeof(MarketplaceDbContext).Assembly.FullName);
                npgsql.UseVector();
            })
            .Options;

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
