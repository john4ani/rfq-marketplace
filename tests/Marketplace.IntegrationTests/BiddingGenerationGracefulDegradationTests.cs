using Marketplace.Application.Bidding;
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

// Regression: auto-bid generation must degrade gracefully when embeddings are not configured
// (missing OpenAI key), generating the proposal from the LLM without CompanyVault RAG context
// instead of failing the request with a 500.
public sealed class BiddingGenerationGracefulDegradationTests
{
    [Fact]
    public async Task GenerateForTenderSucceedsWithoutEmbeddingsWhenTheyAreUnavailable()
    {
        await using PostgreSqlContainer postgres = new PostgreSqlBuilder("pgvector/pgvector:pg17")
            .WithDatabase("marketplace_bidding_degrade_test")
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

        var tender = SeapTenderMapper.MapNotice(new SeapTenderNoticeDto(
            "CN-DEGRADE-1", "Achiziție consumabile", null, ["33652100-6"], 1000m, "RON",
            "Entitate Test", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(30), []), DateTimeOffset.UtcNow);
        db.SeapTenders.Add(tender);
        await db.SaveChangesAsync();

        BiddingGenerationService service = new(
            db,
            new StaticMockLlmProvider(StaticMockLlmProvider.RomanianProcurementExpertProposal),
            new DocumentGenerator(),
            new UnavailableEmbeddingService(),
            Options.Create(new AutoBiddingOptions { IsShadowMode = true }),
            Options.Create(new LocalStorageOptions { UseLocalStorage = false }),
            new MemoryStorageService(),
            TimeProvider.System,
            NullLogger<BiddingGenerationService>.Instance);

        BidGenerationResult result = await service.GenerateForTenderAsync(tender.Id, CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(result.ProposalTitle));
        Assert.StartsWith("memory://bids/", result.TechnicalProposalUrl, StringComparison.Ordinal);
        Assert.StartsWith("memory://bids/", result.BillOfQuantitiesUrl, StringComparison.Ordinal);

        SeapTender saved = await db.SeapTenders.SingleAsync(item => item.Id == tender.Id);
        Assert.Equal("BidGenerated", saved.Status.ToString());
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
    }
}
