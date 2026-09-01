using Marketplace.Application.Bidding;
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

// Regression: auto-bid generation must degrade gracefully when embeddings are not configured
// (missing OpenAI key), generating the proposal from the LLM without CompanyVault RAG context
// instead of failing the request with a 500.
[Collection(IntegrationTestGroup.Name)]
public sealed class BiddingGenerationGracefulDegradationTests : IntegrationTestBase
{
    [Fact]
    public async Task GenerateForTenderSucceedsWithoutEmbeddingsWhenTheyAreUnavailable()
    {
        DbContextOptions<MarketplaceDbContext> options = TestDatabase.CreateOptions();
        await using MarketplaceDbContext db = new(options);
        await db.Database.MigrateAsync();

        var tender = SeapTenderMapper.MapNotice(new SeapTenderNoticeDto(
            "CN-DEGRADE-1", "Achiziție consumabile", null, ["33652100-6"], 1000m, "RON",
            "Entitate Test", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(30), []), DateTimeOffset.UtcNow);
        db.SeapTenders.Add(tender);
        await db.SaveChangesAsync();

        BiddingGenerationService service = new(
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

        BidGenerationResult result = await service.GenerateForTenderAsync(null, tender.Id, CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(result.ProposalTitle));
        Assert.StartsWith("memory://bids/", result.TechnicalProposalUrl, StringComparison.Ordinal);
        Assert.StartsWith("memory://bids/", result.BillOfQuantitiesUrl, StringComparison.Ordinal);

        SeapTender saved = await db.SeapTenders.SingleAsync(item => item.Id == tender.Id);
        Assert.Equal("BidGenerated", saved.Status.ToString());
    }

    [Fact]
    public async Task ListTendersFiltersBySelectedCategoryCpvPrefixes()
    {
        DbContextOptions<MarketplaceDbContext> options = TestDatabase.CreateOptions();
        await using MarketplaceDbContext db = new(options);
        await db.Database.MigrateAsync();

        // The AddCategoryCpvPrefixes migration seeds constructii = ["45","44"], transport-logistica = ["60"].
        Guid constructiiId = await db.Categories.Where(c => c.Slug == "constructii").Select(c => c.Id).SingleAsync();
        Guid transportId = await db.Categories.Where(c => c.Slug == "transport-logistica").Select(c => c.Id).SingleAsync();

        DateTimeOffset now = DateTimeOffset.UtcNow;
        db.SeapTenders.AddRange(
            SeapTenderMapper.MapNotice(new SeapTenderNoticeDto("CN-CONSTRUCT-1", "Lucrări de construcții", null, ["45200000-1"], 1000m, "RON", "Autoritate", now, now.AddDays(30), []), now),
            SeapTenderMapper.MapNotice(new SeapTenderNoticeDto("CN-TRANSPORT-1", "Transport mărfuri", null, ["60100000-9"], 500m, "RON", "Autoritate", now, now.AddDays(30), []), now),
            SeapTenderMapper.MapNotice(new SeapTenderNoticeDto("CN-MEDICAL-1", "Medicamente", null, ["33652100-6"], 200m, "RON", "Autoritate", now, now.AddDays(30), []), now));
        await db.SaveChangesAsync();

        BiddingGenerationService service = new(
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

        IReadOnlyCollection<SeapTender> constructionOnly = await service.ListTendersAsync([constructiiId], null, null, CancellationToken.None);
        Assert.Single(constructionOnly);
        Assert.Equal("CN-CONSTRUCT-1", constructionOnly.Single().SeapExternalId);

        IReadOnlyCollection<SeapTender> all = await service.ListTendersAsync(null, null, null, CancellationToken.None);
        Assert.Equal(3, all.Count);
    }

    private sealed class UnavailableEmbeddingService : IEmbeddingService
    {
        public Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Cheia pentru embeddings OpenAI nu este configurată.");
    }

    private sealed class MemoryStorageService : IStorageService
    {
        public Task<string> UploadAsync(string bucket, string key, string fileName, Stream content, string contentType, CancellationToken cancellationToken) =>
            Task.FromResult($"memory://{key}/{fileName}");
        public Task<StorageObject?> OpenReadAsync(string reference, CancellationToken cancellationToken) =>
            Task.FromResult<StorageObject?>(null);
    }
}
