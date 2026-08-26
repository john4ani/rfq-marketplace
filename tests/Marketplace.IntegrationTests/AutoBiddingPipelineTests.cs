using System.Text;
using Marketplace.Application.AutoBidding;
using Marketplace.Domain.Profiles;
using Marketplace.Domain.Vault;
using Marketplace.Infrastructure.AutoBidding;
using Marketplace.Infrastructure.Bidding;
using Marketplace.Infrastructure.Documents;
using Marketplace.Infrastructure.Identity;
using Marketplace.Infrastructure.Persistence;
using Marketplace.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Testcontainers.PostgreSql;

namespace Marketplace.IntegrationTests;

// End-to-end guardrails for the Caiet de sarcini auto-bidding pipeline:
// PDF -> object storage (local in tests) -> Markdown via (stubbed) Azure Document Intelligence ->
// persisted Markdown -> LLM proposal -> .docx/.xlsx stored.
public sealed class AutoBiddingPipelineTests
{
    [Fact]
    public async Task ProcessSpecificationPersistsPdfMarkdownAndGeneratesProposal()
    {
        await using PostgreSqlContainer postgres = new PostgreSqlBuilder("pgvector/pgvector:pg17")
            .WithDatabase("marketplace_autobid_pipeline_test")
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

        Guid companyId = Guid.NewGuid();
        Guid actorId = Guid.NewGuid();
        db.Users.Add(new ApplicationUser
        {
            Id = companyId,
            UserName = "spec-furnizor",
            NormalizedUserName = "SPEC-FURNIZOR",
            Email = "spec@example.test",
            NormalizedEmail = "SPEC@EXAMPLE.TEST",
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString(),
            ConcurrencyStamp = Guid.NewGuid().ToString()
        });
        db.SupplierProfiles.Add(SupplierProfile.Create(companyId, "Furnizor Test SRL", "J40/1/2026", null,
            "Persoană Contact", "spec@example.test", "0711111111", null,
            new GeographicLocation("Strada 1", "Suceava", "Suceava", "România", 47.7m, 25.8m), 50, null, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();

        string tempRoot = Path.Combine(Path.GetTempPath(), "compaspot-autobid-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            AutoBiddingService service = new(
                db,
                new LocalFileStorageService(Options.Create(new LocalStorageOptions { UseLocalStorage = true, LocalPath = tempRoot }), NullLogger<LocalFileStorageService>.Instance),
                new StubExtractionService(),
                new StaticMockLlmProvider(StaticMockLlmProvider.RomanianProcurementExpertProposal),
                new DocumentGenerator(),
                Options.Create(new AutoBiddingOptions { IsShadowMode = true }),
                Options.Create(new LocalStorageOptions { UseLocalStorage = true, LocalPath = tempRoot }),
                TimeProvider.System,
                NullLogger<AutoBiddingService>.Instance);

            using MemoryStream pdf = new(Encoding.UTF8.GetBytes("%PDF-caiet-de-sarcini"));

            SpecificationBidResult result = await service.ProcessSpecificationAsync(
                companyId, "caiet-de-sarcini.pdf", pdf, actorId, CancellationToken.None);

            Assert.StartsWith("local://specifications/", result.DocumentUrl, StringComparison.Ordinal);
            Assert.Single(Directory.GetFiles(Path.Combine(tempRoot, "specifications", companyId.ToString())));
            Assert.Contains("Tabel preturi", result.Markdown, StringComparison.Ordinal);
            Assert.Contains("| Produs | Pret |", result.Markdown, StringComparison.Ordinal);
            Assert.False(string.IsNullOrWhiteSpace(result.ProposalTitle));
            Assert.StartsWith("local://bids/", result.TechnicalProposalUrl, StringComparison.Ordinal);
            Assert.StartsWith("local://bids/", result.BillOfQuantitiesUrl, StringComparison.Ordinal);
            Assert.Equal(2, Directory.GetFiles(Path.Combine(tempRoot, "bids", result.DocumentId.ToString())).Length);

            VaultDocument saved = await db.VaultDocuments.SingleAsync(document => document.Id == result.DocumentId);
            Assert.Equal(VaultDocumentType.Specification, saved.DocumentType);
            Assert.True(saved.Extracted);
            Assert.Equal(result.Markdown, saved.ExtractedMarkdown);
            Assert.Equal(result.ProposalTitle, saved.ProposalTitle);
            Assert.Equal(result.TechnicalProposalUrl, saved.TechnicalProposalUrl);
            Assert.Equal(result.BillOfQuantitiesUrl, saved.BillOfQuantitiesUrl);
        }
        finally
        {
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task GenerateFromSpecificationRequiresExtractedMarkdown()
    {
        await using PostgreSqlContainer postgres = new PostgreSqlBuilder("pgvector/pgvector:pg17")
            .WithDatabase("marketplace_autobid_validate_test")
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

        Guid companyId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        db.Users.Add(new ApplicationUser
        {
            Id = companyId,
            UserName = "spec2-furnizor",
            NormalizedUserName = "SPEC2-FURNIZOR",
            Email = "spec2@example.test",
            NormalizedEmail = "SPEC2@EXAMPLE.TEST",
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString(),
            ConcurrencyStamp = Guid.NewGuid().ToString()
        });
        db.SupplierProfiles.Add(SupplierProfile.Create(companyId, "Furnizor 2 SRL", "J40/2/2026", null,
            "Persoană Contact", "spec2@example.test", "0712222222", null,
            new GeographicLocation("Strada 2", "Suceava", "Suceava", "România", 47.7m, 25.8m), 50, null, now));

        CompanyVault vault = CompanyVault.Create(companyId, now);
        vault.AddDocument("caiet.pdf", "memory://specifications/caiet.pdf", VaultDocumentType.Specification, companyId, now);
        db.CompanyVaults.Add(vault);
        await db.SaveChangesAsync();
        Guid documentId = vault.Documents.Single().Id;

        AutoBiddingService service = new(
            db,
            new MemoryStorageService(),
            new StubExtractionService(),
            new StaticMockLlmProvider(StaticMockLlmProvider.RomanianProcurementExpertProposal),
            new DocumentGenerator(),
            Options.Create(new AutoBiddingOptions { IsShadowMode = true }),
            Options.Create(new LocalStorageOptions { UseLocalStorage = false }),
            TimeProvider.System,
            NullLogger<AutoBiddingService>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GenerateFromSpecificationAsync(companyId, documentId, CancellationToken.None));
    }

    [Fact]
    public void MarkdownConverterBuildsPipeTablesAndPageHeadings()
    {
        DocumentExtractionResult extraction = new(
            "Text pagina 1\ncontinut",
            [new ExtractedPage(1, "Text pagina 1\ncontinut",
                [new ExtractedTable("Tabel preturi", [["Produs", "Pret"], ["Ciment", "120"], ["Beton", "250"]])])]);

        string markdown = MarkdownConverter.ToMarkdown(extraction);

        Assert.Contains("## Pagina 1", markdown, StringComparison.Ordinal);
        Assert.Contains("| Produs | Pret |", markdown, StringComparison.Ordinal);
        Assert.Contains("| --- | --- |", markdown, StringComparison.Ordinal);
        Assert.Contains("| Ciment | 120 |", markdown, StringComparison.Ordinal);
        Assert.Contains("| Beton | 250 |", markdown, StringComparison.Ordinal);
        Assert.Contains("Text pagina 1", markdown, StringComparison.Ordinal);
    }

    private sealed class StubExtractionService : IDocumentExtractionService
    {
        public Task<DocumentExtractionResult> ExtractAsync(Stream document, string fileName, CancellationToken cancellationToken) =>
            Task.FromResult(new DocumentExtractionResult(
                "Text pagina 1\ncontinut",
                [new ExtractedPage(1, "Text pagina 1\ncontinut",
                    [new ExtractedTable("Tabel preturi", [["Produs", "Pret"], ["Ciment", "120"]])])]));
    }

    private sealed class MemoryStorageService : IStorageService
    {
        public Task<string> UploadAsync(string bucket, string key, string fileName, Stream content, string contentType, CancellationToken cancellationToken) =>
            Task.FromResult($"memory://{key}/{fileName}");
    }
}
