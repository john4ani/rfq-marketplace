using Marketplace.Application.Seap;
using Marketplace.Domain.Seap;
using Marketplace.Domain.Suppliers;
using Marketplace.Infrastructure.Bidding;
using Marketplace.Infrastructure.Persistence;
using Marketplace.Infrastructure.Persistence.Configurations;
using Marketplace.Infrastructure.Seap;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.PostgreSql;

namespace Marketplace.IntegrationTests;

// SeapCategorizer: CPV code → CompaSpot category. Guards the persistent CPV→category cache (the LLM is
// called once per standardized CPV code, never per tender), the auto-creation of missing categories under
// the General Procurement root, and the graceful null fallback on any LLM failure.
public sealed class SeapCategorizerTests
{
    [Fact]
    public async Task ExistingCategoryIsResolvedAndCachedForTheSameCpvCode()
    {
        await using PostgreSqlContainer postgres = new PostgreSqlBuilder("pgvector/pgvector:pg17")
            .WithDatabase("marketplace_categorizer_test")
            .Build();
        await postgres.StartAsync();

        DbContextOptions<MarketplaceDbContext> options = NewOptions(postgres);
        await using MarketplaceDbContext db = new(options);
        await db.Database.MigrateAsync();

        Guid constructiiId = await db.Categories.Where(category => category.Slug == "constructii").Select(category => category.Id).SingleAsync();
        CountingCategoryLlmProvider llm = new("""{"category":"Construcții"}""");
        SeapCategorizerService service = new(db, llm, NullLogger<SeapCategorizerService>.Instance);

        SeapCategorizationResult? first = await service.CategorizeAsync("45342000-6", "Montare de garduri", "Lucrări gard", CancellationToken.None);
        Assert.NotNull(first);
        Assert.Equal(constructiiId, first.CategoryId);
        Assert.False(first.FromCache);
        Assert.False(first.NewCategory);
        Assert.Equal(1, llm.CallCount);
        await db.SaveChangesAsync();

        // Same standardized CPV code → resolved from the persistent cache, no second LLM call.
        SeapCategorizationResult? cached = await service.CategorizeAsync("45342000-6", "Montare de garduri", "Alt anunț gard", CancellationToken.None);
        Assert.NotNull(cached);
        Assert.Equal(constructiiId, cached.CategoryId);
        Assert.True(cached.FromCache);
        Assert.Equal(1, llm.CallCount);

        Assert.Single(await db.CpvCategoryMappings.ToArrayAsync());
    }

    [Fact]
    public async Task UnknownCategoryIsAutoCreatedUnderGeneralProcurementRoot()
    {
        await using PostgreSqlContainer postgres = new PostgreSqlBuilder("pgvector/pgvector:pg17")
            .WithDatabase("marketplace_categorizer_autocreate_test")
            .Build();
        await postgres.StartAsync();

        DbContextOptions<MarketplaceDbContext> options = NewOptions(postgres);
        await using MarketplaceDbContext db = new(options);
        await db.Database.MigrateAsync();

        SeapCategorizerService service = new(db, new CountingCategoryLlmProvider("""{"category":"Montare de garduri"}"""), NullLogger<SeapCategorizerService>.Instance);

        SeapCategorizationResult? result = await service.CategorizeAsync("45342000-6", "Montare de garduri", "Împrejmuire", CancellationToken.None);
        Assert.NotNull(result);
        Assert.True(result.NewCategory);
        Assert.False(result.FromCache);
        Assert.Equal("Montare de garduri", result.CategoryName);
        await db.SaveChangesAsync();

        Category created = await db.Categories.SingleAsync(category => category.Id == result.CategoryId);
        Assert.Equal("Montare de garduri", created.Name);
        Assert.Equal("montare-de-garduri", created.Slug);
        Assert.True(created.IsActive);
        // Auto-created under the General Procurement root, never inside the Construction domain.
        Assert.Equal(CategoryConfiguration.GeneralProcurementRootId, created.ParentId);
    }

    [Fact]
    public async Task LlmOutputVariantsAreToleratedAndFailuresDegradeToNull()
    {
        await using PostgreSqlContainer postgres = new PostgreSqlBuilder("pgvector/pgvector:pg17")
            .WithDatabase("marketplace_categorizer_degrade_test")
            .Build();
        await postgres.StartAsync();

        DbContextOptions<MarketplaceDbContext> options = NewOptions(postgres);
        await using MarketplaceDbContext db = new(options);
        await db.Database.MigrateAsync();

        // Markdown-wrapped JSON still parses.
        SeapCategorizerService markdown = new(db, new CountingCategoryLlmProvider("```json\n{\"category\":\"Izolații\"}\n```"), NullLogger<SeapCategorizerService>.Instance);
        SeapCategorizationResult? wrapped = await markdown.CategorizeAsync("45321000-1", "Izolații termice", "Izolație", CancellationToken.None);
        Assert.NotNull(wrapped);
        Assert.Equal(await db.Categories.Where(category => category.Slug == "izolatii").Select(category => category.Id).SingleAsync(), wrapped.CategoryId);

        // Garbage output → null (tender still imports, uncategorized).
        SeapCategorizerService garbage = new(db, new CountingCategoryLlmProvider("nu sunt JSON"), NullLogger<SeapCategorizerService>.Instance);
        Assert.Null(await garbage.CategorizeAsync("44000000-0", "Constructii", "X", CancellationToken.None));

        // Throwing LLM → null (graceful degradation).
        SeapCategorizerService throwing = new(db, new ThrowingLlmProvider(), NullLogger<SeapCategorizerService>.Instance);
        Assert.Null(await throwing.CategorizeAsync("33000000-0", "Echipament medical", "Y", CancellationToken.None));

        await db.SaveChangesAsync();
        // Only the successful markdown-wrapped categorization cached a mapping; garbage and throwing
        // LLM outputs degraded to null without writing anything.
        Assert.Equal(["45321000-1"], await db.CpvCategoryMappings.Select(mapping => mapping.CpvCode).ToArrayAsync());
    }

    private static DbContextOptions<MarketplaceDbContext> NewOptions(PostgreSqlContainer postgres) =>
        new DbContextOptionsBuilder<MarketplaceDbContext>()
            .UseNpgsql(postgres.GetConnectionString(), npgsql =>
            {
                npgsql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(5), null);
                npgsql.MigrationsAssembly(typeof(MarketplaceDbContext).Assembly.FullName);
                npgsql.UseVector();
            })
            .Options;

    private sealed class CountingCategoryLlmProvider(string jsonResponse) : ILlmProvider
    {
        public int CallCount { get; private set; }
        public Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(jsonResponse);
        }
    }

    private sealed class ThrowingLlmProvider : ILlmProvider
    {
        public Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("LLM indisponibil.");
    }
}
