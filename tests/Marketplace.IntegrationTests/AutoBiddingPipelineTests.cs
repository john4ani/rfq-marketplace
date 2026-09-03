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

namespace Marketplace.IntegrationTests;

// End-to-end guardrails for the Caiet de sarcini auto-bidding pipeline:
// PDF -> object storage (local in tests) -> Markdown via (stubbed) Azure Document Intelligence ->
// persisted Markdown -> LLM proposal -> .docx/.xlsx stored.
[Collection(IntegrationTestGroup.Name)]
public sealed class AutoBiddingPipelineTests : IntegrationTestBase
{
    [Fact]
    public async Task ProcessSpecificationPersistsPdfMarkdownAndGeneratesProposal()
    {
        DbContextOptions<MarketplaceDbContext> options = TestDatabase.CreateOptions();
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
    public async Task ProcessSpecificationThrowsWhenOcrReturnsInsufficientText()
    {
        DbContextOptions<MarketplaceDbContext> options = TestDatabase.CreateOptions();
        await using MarketplaceDbContext db = new(options);
        await db.Database.MigrateAsync();

        Guid companyId = Guid.NewGuid();
        db.Users.Add(new ApplicationUser
        {
            Id = companyId,
            UserName = "spec-ocr", NormalizedUserName = "SPEC-OCR",
            Email = "spec-ocr@example.test", NormalizedEmail = "SPEC-OCR@EXAMPLE.TEST",
            EmailConfirmed = true, SecurityStamp = Guid.NewGuid().ToString(), ConcurrencyStamp = Guid.NewGuid().ToString()
        });
        db.SupplierProfiles.Add(SupplierProfile.Create(companyId, "Furnizor OCR SRL", "J40/3/2026", null,
            "Persoană Contact", "spec-ocr@example.test", "0713333333", null,
            new GeographicLocation("Strada 1", "Suceava", "Suceava", "România", 47.7m, 25.8m), 50, null, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();

        string tempRoot = Path.Combine(Path.GetTempPath(), "compaspot-autobid-ocr-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            AutoBiddingService service = new(
                db,
                new LocalFileStorageService(Options.Create(new LocalStorageOptions { UseLocalStorage = true, LocalPath = tempRoot }), NullLogger<LocalFileStorageService>.Instance),
                new ShortTextExtractionService(),
                new StaticMockLlmProvider(StaticMockLlmProvider.RomanianProcurementExpertProposal),
                new DocumentGenerator(),
                Options.Create(new AutoBiddingOptions { IsShadowMode = true }),
                Options.Create(new LocalStorageOptions { UseLocalStorage = true, LocalPath = tempRoot }),
                TimeProvider.System,
                NullLogger<AutoBiddingService>.Instance);

            using MemoryStream pdf = new(Encoding.UTF8.GetBytes("%PDF-scan-doar-imagini"));
            AutoBiddingPipelineException exception = await Assert.ThrowsAsync<AutoBiddingPipelineException>(() =>
                service.ProcessSpecificationAsync(companyId, "scan.pdf", pdf, companyId, CancellationToken.None));
            Assert.Contains("insuficient", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(422, exception.StatusCode);
        }
        finally
        {
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task GenerateFromSpecificationRequiresExtractedMarkdown()
    {
        DbContextOptions<MarketplaceDbContext> options = TestDatabase.CreateOptions();
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

    [Fact]
    public void MergeAndOrderPagesDeduplicatesAndKeepsMoreCompleteExtraction()
    {
        // Two chunks may return the same page with different completeness (e.g. full-doc truncation overlap).
        List<ExtractedPage> chunks =
        [
            new(3, "Pagina 3 scurta", []),
            new(1, "Pagina 1", []),
            new(2, "Pagina 2", []),
            new(3, "Pagina 3 completa cu Y BOI si TOTAL GENERAL", [])
        ];

        List<ExtractedPage> merged = AzureDocumentIntelligenceExtractionService.MergeAndOrderPages(chunks);

        Assert.Equal(3, merged.Count);
        Assert.Equal(1, merged[0].PageNumber);
        Assert.Equal(2, merged[1].PageNumber);
        Assert.Equal(3, merged[2].PageNumber);
        // Page 3 keeps the more complete text (the deduplication winner).
        Assert.Equal("Pagina 3 completa cu Y BOI si TOTAL GENERAL", merged[2].Text);
    }

    [Fact]
    public void PipelineProgressStoreTracksStagesAndBoundaries()
    {
        PipelineProgressStore store = new();
        Guid requestId = Guid.NewGuid();

        Assert.Null(store.Find(requestId));
        store.Report(requestId, PipelineStage.Extracting, "Se extrag tabelele…");
        Assert.Equal(PipelineStage.Extracting, store.Find(requestId)!.Stage);
        store.Report(requestId, PipelineStage.Generating, "Claude redactează…");
        Assert.Equal(PipelineStage.Generating, store.Find(requestId)!.Stage);
        store.Report(requestId, PipelineStage.Failed, "Eroare");
        Assert.Equal(PipelineStage.Failed, store.Find(requestId)!.Stage);
    }

    [Fact]
    public void ParserAcceptsCompleteJsonWhenClosingFenceIsMissing()
    {
        // Some models emit the opening ```json fence but omit the closing one; the JSON body itself is
        // complete and must parse (Normalize keeps everything after the opening fence line).
        string fenced = "```json\n" + StaticMockLlmProvider.RomanianProcurementExpertProposal.Trim();
        BiddingProposal proposal = BiddingJsonParser.Parse(fenced);
        Assert.Equal("Ofertă tehnică — Lucrări de construcții", proposal.Title);
    }

    [Fact]
    public void ParserReportsTruncatedJsonAsActionableError()
    {
        // Claude sometimes emits ```json ... without a closing fence when the response is truncated.
        string truncated = "```json\n{\"title\":\"Ofertă\",\"technicalProposal\":{\"sections\":[{\"heading\":\"H\",\"content\":\"C\"}]},";
        AutoBiddingPipelineException exception = Assert.Throws<AutoBiddingPipelineException>(() => BiddingJsonParser.Parse(truncated));
        Assert.Contains("trunchiat", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(422, exception.StatusCode);
    }

    private sealed class StubExtractionService : IDocumentExtractionService
    {
        public Task<DocumentExtractionResult> ExtractAsync(Stream document, string fileName, CancellationToken cancellationToken) =>
            Task.FromResult(new DocumentExtractionResult(
                LongText,
                [new ExtractedPage(1, LongText,
                    [new ExtractedTable("Tabel preturi", [["Produs", "Pret"], ["Ciment", "120"]])])]));
    }

    /// <summary>Realistic OCR output long enough to pass the 200-char pipeline guard.</summary>
    private const string LongText =
        "Text pagina 1\ncontinut\n" +
        "Cerința: executare lucrări conform proiectului tehnic, normelor în vigoare și caietului de sarcini. " +
        "Materialele utilizate trebuie să fie certificate și livrate cu documente de proveniență. " +
        "Organizarea de șantier se realizează conform legislației aplicabile, cu păstrarea accesului publicului. " +
        "Termenul de execuție este de 12 luni de la emiterea ordinului de începere a lucrărilor.";

    /// <summary>OCR output far below the 200-char guard (image-only scan).</summary>
    private sealed class ShortTextExtractionService : IDocumentExtractionService
    {
        public Task<DocumentExtractionResult> ExtractAsync(Stream document, string fileName, CancellationToken cancellationToken) =>
            Task.FromResult(new DocumentExtractionResult("Text scurt.", [new ExtractedPage(1, "Text scurt.", [])]));
    }

    private sealed class MemoryStorageService : IStorageService
    {
        public Task<string> UploadAsync(string bucket, string key, string fileName, Stream content, string contentType, CancellationToken cancellationToken) =>
            Task.FromResult($"memory://{key}/{fileName}");
        public Task<StorageObject?> OpenReadAsync(string reference, CancellationToken cancellationToken) =>
            Task.FromResult<StorageObject?>(null);
        public Task DeleteReferenceAsync(string reference, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
