using Marketplace.Application.Bidding;
using Marketplace.Domain.Seap;
using Marketplace.Domain.Vault;
using Marketplace.Infrastructure.Embeddings;
using Marketplace.Infrastructure.Persistence;
using Marketplace.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pgvector;

namespace Marketplace.Infrastructure.Bidding;

/// <summary>
/// Orchestrates: embed tender requirements → pgvector RAG over CompanyVault chunks → LLM proposal →
/// parse JSON → .docx/.xlsx → S3. Shadow Mode only affects visibility/charging (handled by the API),
/// never the generation itself.
/// </summary>
public sealed partial class BiddingGenerationService : IBiddingGenerationService
{
    private const string RomanianProcurementExpertSystemPrompt =
        "Ești un expert român în achiziții publice. Analizează cerințele anunțului și datele firmei " +
        "și generează o propunere tehnică și o listă de cantități în JSON valid, în limba română. " +
        "Nu inventa certificări, prețuri sau capacități care nu sunt susținute de datele furnizate.";

    private readonly MarketplaceDbContext dbContext;
    private readonly ILlmProvider llmProvider;
    private readonly IDocumentGenerator documentGenerator;
    private readonly IEmbeddingService embeddingService;
    private readonly IStorageService storage;
    private readonly bool useLocalStorage;
    private readonly AutoBiddingOptions settings;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<BiddingGenerationService> logger;

    public BiddingGenerationService(
        MarketplaceDbContext dbContext,
        ILlmProvider llmProvider,
        IDocumentGenerator documentGenerator,
        IEmbeddingService embeddingService,
        IOptions<AutoBiddingOptions> options,
        IOptions<LocalStorageOptions> localStorage,
        IStorageService storage,
        TimeProvider timeProvider,
        ILogger<BiddingGenerationService> logger)
    {
        this.dbContext = dbContext;
        this.llmProvider = llmProvider;
        this.documentGenerator = documentGenerator;
        this.embeddingService = embeddingService;
        this.storage = storage;
        useLocalStorage = localStorage.Value.UseLocalStorage;
        settings = options.Value;
        this.timeProvider = timeProvider;
        this.logger = logger;
    }


    public async Task<BidGenerationResult> GenerateForTenderAsync(Guid tenderId, CancellationToken cancellationToken)
    {
        SeapTender? tender = await dbContext.SeapTenders.Include(item => item.Attachments)
            .SingleOrDefaultAsync(item => item.Id == tenderId, cancellationToken)
            ?? throw new KeyNotFoundException("Anunțul SEAP nu există.");

        string requirements = BuildRequirementsText(tender);
        string vaultContext = await BuildVaultContextAsync(requirements, cancellationToken);

        string userPrompt = string.IsNullOrWhiteSpace(vaultContext)
            ? $"Cerințele anunțului:\n{requirements}\n\nDate despre firmă: (nu sunt disponibile documente în CompanyVault)."
            : $"Cerințele anunțului:\n{requirements}\n\nDate despre firmă (fragmente relevante din CompanyVault):\n{vaultContext}";
        string llmJson = await llmProvider.CompleteAsync(RomanianProcurementExpertSystemPrompt, userPrompt, cancellationToken);
        BiddingProposal proposal = BiddingJsonParser.Parse(llmJson);

        GeneratedDocument technical = await documentGenerator.GenerateTechnicalProposalAsync(proposal, cancellationToken);
        GeneratedDocument boq = await documentGenerator.GenerateBillOfQuantitiesAsync(proposal, cancellationToken);
        string technicalUrl = await UploadDocumentAsync(tender.Id, technical, cancellationToken);
        string boqUrl = await UploadDocumentAsync(tender.Id, boq, cancellationToken);

        tender.RecordGeneratedBid(proposal.Title, technicalUrl, boqUrl, timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
        LogBidGenerated(logger, tender.Id, settings.IsShadowMode);
        return new(tender.Id, proposal.Title, technicalUrl, boqUrl, settings.IsShadowMode, timeProvider.GetUtcNow());
    }

    public Task<IReadOnlyCollection<SeapTender>> ListTendersAsync(CancellationToken cancellationToken) =>
        dbContext.SeapTenders.AsNoTracking().Include(item => item.Attachments)
            .OrderByDescending(item => item.PublicationDate)
            .ToArrayAsync(cancellationToken)
            .ContinueWith(task => (IReadOnlyCollection<SeapTender>)task.Result, cancellationToken);

    public Task<SeapTender?> GetTenderAsync(Guid tenderId, CancellationToken cancellationToken) =>
        dbContext.SeapTenders.AsNoTracking().Include(item => item.Attachments)
            .SingleOrDefaultAsync(item => item.Id == tenderId, cancellationToken);

    private static string BuildRequirementsText(SeapTender tender)
    {
        List<string> parts = [tender.Title];
        if (!string.IsNullOrWhiteSpace(tender.Description)) parts.Add(tender.Description);
        if (tender.CpvCodes.Count > 0) parts.Add($"Coduri CPV: {string.Join(", ", tender.CpvCodes)}");
        if (tender.EstimatedValue.HasValue) parts.Add($"Valoare estimată: {tender.EstimatedValue.Value:N2} {tender.Currency}");
        parts.Add($"Instituție contractantă: {tender.BuyerName}");
        parts.Add($"Termen depunere: {tender.Deadline:dd.MM.yyyy}");
        return string.Join("\n", parts);
    }

    /// <summary>
    /// RAG over CompanyVault chunks. When embeddings are not configured (no OpenAI key), the flow
    /// degrades to a plain LLM generation without company context instead of failing the request —
    /// mirroring CompanyVaultService.TryEmbedAsync.
    /// </summary>
    private async Task<string> BuildVaultContextAsync(string requirements, CancellationToken cancellationToken)
    {
        try
        {
            float[] queryEmbedding = await embeddingService.EmbedAsync(requirements, cancellationToken);
            Vector vector = new(queryEmbedding);
            List<DocumentChunk> chunks = await dbContext.DocumentChunks
                .FromSqlInterpolated($"""
                    SELECT * FROM "DocumentChunks"
                    ORDER BY "Embedding" <=> {vector}
                    LIMIT {Math.Max(1, settings.TopChunks)}
                    """)
                .ToListAsync(cancellationToken);
            return string.Join("\n\n", chunks.Select(chunk => chunk.Content));
        }
        catch (InvalidOperationException exception)
        {
            LogEmbeddingsUnavailable(logger, exception.Message);
            return string.Empty;
        }
    }

    private async Task<string> UploadDocumentAsync(Guid tenderId, GeneratedDocument document, CancellationToken cancellationToken)
    {
        if (useLocalStorage)
            return await storage.UploadAsync("bids", $"bids/{tenderId}", document.FileName,
                new MemoryStream(document.Content), document.ContentType, cancellationToken);
        if (string.IsNullOrWhiteSpace(settings.DocumentsBucket))
            return $"memory://bids/{tenderId}/{document.FileName}";
        return await storage.UploadAsync(settings.DocumentsBucket, $"bids/{tenderId}", document.FileName,
            new MemoryStream(document.Content), document.ContentType, cancellationToken);
    }

    [LoggerMessage(EventId = 5301, Level = LogLevel.Warning, Message = "Embeddings unavailable ({Reason}); generating the bid without CompanyVault context.")]
    private static partial void LogEmbeddingsUnavailable(ILogger logger, string reason);
    [LoggerMessage(EventId = 5302, Level = LogLevel.Information, Message = "Auto-bid generated for SEAP tender {TenderId} (shadow mode: {ShadowMode}).")]
    private static partial void LogBidGenerated(ILogger logger, Guid tenderId, bool shadowMode);
}

