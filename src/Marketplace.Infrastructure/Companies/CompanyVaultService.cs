using Marketplace.Application.Companies;

using Marketplace.Domain.Profiles;
using Marketplace.Domain.Vault;
using Marketplace.Infrastructure.Bidding;
using Marketplace.Infrastructure.Documents;
using Marketplace.Infrastructure.Embeddings;
using Marketplace.Infrastructure.Persistence;
using Marketplace.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Marketplace.Infrastructure.Companies;

/// <summary>
/// Company Vault: upload â†’ persist metadata â†’ optional Azure OCR extraction â†’ overlapping chunking â†’
/// optional OpenAI embeddings â†’ pgvector chunks. Every external step is interface-gated so the flow
/// degrades gracefully when credentials are missing.
/// </summary>
public sealed partial class CompanyVaultService : ICompanyVaultService
{
    private readonly MarketplaceDbContext dbContext;
    private readonly IStorageService storage;
    private readonly bool useLocalStorage;
    private readonly AutoBiddingOptions autoBiddingOptions;
    private readonly IDocumentExtractionService extractionService;
    private readonly IEmbeddingService embeddingService;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<CompanyVaultService> logger;

    public CompanyVaultService(
        MarketplaceDbContext dbContext,
        IStorageService storage,
        IOptions<LocalStorageOptions> localStorage,
        IOptions<AutoBiddingOptions> autoBiddingOptions,
        IDocumentExtractionService extractionService,
        IEmbeddingService embeddingService,
        TimeProvider timeProvider,
        ILogger<CompanyVaultService> logger)
    {
        this.dbContext = dbContext;
        this.storage = storage;
        useLocalStorage = localStorage.Value.UseLocalStorage;
        this.autoBiddingOptions = autoBiddingOptions.Value;
        this.extractionService = extractionService;
        this.embeddingService = embeddingService;
        this.timeProvider = timeProvider;
        this.logger = logger;
    }


    public async Task<IReadOnlyCollection<VaultDocumentView>> GetVaultAsync(Guid companyId, CancellationToken cancellationToken)
    {
        Guid? vaultId = await dbContext.CompanyVaults.AsNoTracking()
            .Where(vault => vault.SupplierId == companyId).Select(vault => (Guid?)vault.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (vaultId is null) return [];
        VaultDocument[] documents = await dbContext.VaultDocuments.AsNoTracking()
            .Where(document => document.VaultId == vaultId.Value).OrderByDescending(document => document.UploadedAt)
            .ToArrayAsync(cancellationToken);
        Guid[] documentIds = documents.Select(document => document.Id).ToArray();
        Dictionary<Guid, int> chunkCounts = await dbContext.DocumentChunks.AsNoTracking()
            .Where(chunk => documentIds.Contains(chunk.DocumentId)).GroupBy(chunk => chunk.DocumentId)
            .ToDictionaryAsync(group => group.Key, group => group.Count(), cancellationToken);
        return documents.Select(document => new VaultDocumentView(document.Id, document.FileName,
            document.DocumentType.ToString(), document.Extracted, document.PageCount,
            chunkCounts.GetValueOrDefault(document.Id), document.UploadedAt)).ToArray();
    }

    public async Task<VaultDocumentView?> UploadDocumentAsync(
        Guid companyId, string fileName, Stream content, VaultDocumentType documentType, Guid actorId, CancellationToken cancellationToken)
    {
        if (!await dbContext.SupplierProfiles.AsNoTracking().AnyAsync(profile => profile.Id == companyId, cancellationToken))
            return null;
        DateTimeOffset now = timeProvider.GetUtcNow();
        CompanyVault vault = await dbContext.CompanyVaults.Include(item => item.Documents)
            .SingleOrDefaultAsync(item => item.SupplierId == companyId, cancellationToken)
            ?? CompanyVault.Create(companyId, now);

        using MemoryStream buffer = new();
        await content.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;
        string s3Url = await UploadToStorageAsync(companyId, fileName, buffer, cancellationToken);
        VaultDocument document = vault.AddDocument(fileName, s3Url, documentType, actorId, now);
        // CompanyVault.Create assigns a new Guid, so a fresh vault must be attached explicitly.
        if (dbContext.Entry(vault).State == EntityState.Detached) dbContext.CompanyVaults.Add(vault);

        try
        {
            buffer.Position = 0;
            await ExtractAndEmbedAsync(document, buffer, cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            // Extraction/embeddings not configured or unavailable: the document stays uploaded, unprocessed.
            LogExtractionSkipped(logger, fileName, exception.Message);
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        LogDocumentUploaded(logger, fileName, vault.Id);
        return (await GetVaultAsync(companyId, cancellationToken)).Single(item => item.Id == document.Id);
    }

    private async Task<string> UploadToStorageAsync(Guid companyId, string fileName, Stream content, CancellationToken cancellationToken)
    {
        if (useLocalStorage)
            return await storage.UploadAsync("vault", $"vault/{companyId}", fileName, content, "application/octet-stream", cancellationToken);
        if (string.IsNullOrWhiteSpace(autoBiddingOptions.DocumentsBucket))
            return $"memory://vault/{companyId}/{fileName}";
        return await storage.UploadAsync(autoBiddingOptions.DocumentsBucket, $"vault/{companyId}", fileName, content, "application/octet-stream", cancellationToken);
    }

    private async Task ExtractAndEmbedAsync(VaultDocument document, Stream content, CancellationToken cancellationToken)
    {
        DocumentExtractionResult extraction = await extractionService.ExtractAsync(content, document.FileName, cancellationToken);
        document.SetExtractedMarkdown(MarkdownConverter.ToMarkdown(extraction));
        IReadOnlyCollection<string> chunks = TextChunker.Chunk(extraction.FullText);
        int index = 0;
        foreach (string chunk in chunks)
        {
            float[]? embedding = await TryEmbedAsync(chunk, cancellationToken);
            document.AddChunk(index++, chunk, embedding, timeProvider.GetUtcNow());
        }
        document.MarkExtracted(extraction.Pages.Count);
    }

    private async Task<float[]?> TryEmbedAsync(string text, CancellationToken cancellationToken)
    {
        try
        {
            return await embeddingService.EmbedAsync(text, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return null; // embeddings not configured â€” the chunk is stored without a vector.
        }
    }

    [LoggerMessage(EventId = 5501, Level = LogLevel.Information, Message = "Vault document {FileName} uploaded for vault {VaultId}.")]
    private static partial void LogDocumentUploaded(ILogger logger, string fileName, Guid vaultId);
    [LoggerMessage(EventId = 5502, Level = LogLevel.Warning, Message = "Vault extraction skipped for {FileName}: {Reason}")]
    private static partial void LogExtractionSkipped(ILogger logger, string fileName, string reason);
}


