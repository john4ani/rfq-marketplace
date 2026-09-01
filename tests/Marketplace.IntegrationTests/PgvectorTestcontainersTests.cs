using Marketplace.Domain.Profiles;
using Marketplace.Domain.Vault;
using Marketplace.Infrastructure.Identity;
using Marketplace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pgvector;

namespace Marketplace.IntegrationTests;

// Real PostgreSQL 17 with the pgvector extension via the shared IntegrationTestGroup Testcontainers
// container — never EF InMemory.
[Collection(IntegrationTestGroup.Name)]
public sealed class PgvectorTestcontainersTests : IntegrationTestBase
{
    [Fact]
    public async Task AddDbContextRegistrationKeepsVectorMapping()
    {
        ServiceCollection services = new();
        services.AddDbContext<MarketplaceDbContext>(options => options.UseNpgsql(
            TestDatabase.ConnectionString,
            npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(MarketplaceDbContext).Assembly.FullName);
                npgsql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(5), null);
                npgsql.UseVector();
            }));
        await using ServiceProvider provider = services.BuildServiceProvider();
        await using MarketplaceDbContext db = provider.GetRequiredService<MarketplaceDbContext>();
        // Building the model is what the app host does on first use; it must validate the vector column.
        _ = db.Model;
        Assert.True(db.Database.IsNpgsql());
    }

    [Fact]
    public void WebApplicationFactoryKeepsVectorMapping()
    {
        using MarketplaceWebApplicationFactory factory = new();
        using IServiceScope scope = factory.Services.CreateScope();
        using MarketplaceDbContext db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
        _ = db.Model; // throws if the vector mapping is missing in the hosted app
    }

    private static float[] UnitVector(int axis) =>
        Enumerable.Range(0, 1536).Select(index => index == axis ? 1f : 0f).ToArray();

    [Fact]
    public async Task CosineSearchFindsClosestChunkOnRealPostgresWithVector()
    {
        DbContextOptions<MarketplaceDbContext> options = TestDatabase.CreateOptions();
        await using MarketplaceDbContext db = new(options);
        await db.Database.MigrateAsync();

        DateTimeOffset now = DateTimeOffset.UtcNow;
        Guid supplierId = Guid.NewGuid();
        db.Users.Add(new ApplicationUser
        {
            Id = supplierId,
            UserName = "furnizor-test",
            NormalizedUserName = "FURNIZOR-TEST",
            Email = "furnizor@example.test",
            NormalizedEmail = "FURNIZOR@EXAMPLE.TEST",
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString(),
            ConcurrencyStamp = Guid.NewGuid().ToString()
        });
        db.SupplierProfiles.Add(SupplierProfile.Create(supplierId, "Furnizor Test SRL", "J40/1/2026", null,
            "Persoană Contact", "furnizor@example.test", "0711111111", null,
            new GeographicLocation("Strada 1", "Suceava", "Suceava", "România", 47.7m, 25.8m), 50, null, now));
        CompanyVault vault = CompanyVault.Create(supplierId, now);
        VaultDocument document = vault.AddDocument("oferta.pdf", "s3://bucket/oferta.pdf",
            VaultDocumentType.TechnicalProposal, Guid.NewGuid(), now);
        document.AddChunk(0, "Conținut aproape de interogare", UnitVector(0), now);
        document.AddChunk(1, "Conținut complet diferit", UnitVector(1535), now);
        db.CompanyVaults.Add(vault);
        await db.SaveChangesAsync();

        Vector query = new(UnitVector(0));
        List<DocumentChunk> hits = await db.DocumentChunks
            .FromSqlInterpolated($"""
                SELECT * FROM "DocumentChunks"
                ORDER BY "Embedding" <=> {query}
                LIMIT 1
                """)
            .ToListAsync();
        Assert.Single(hits);
        Assert.Equal("Conținut aproape de interogare", hits[0].Content);
    }
}
