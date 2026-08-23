using Amazon.S3;
using Amazon.S3.Model;
using Marketplace.Application.Bidding;
using Marketplace.Domain.Seap;
using Marketplace.Domain.Vault;
using Marketplace.Infrastructure.Embeddings;
using Marketplace.Infrastructure.Persistence;
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
    private readonly AutoBiddingOptions settings;
    private readonly IAmazonS3 s3;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<BiddingGenerationService> logger;

    public BiddingGenerationService(
        MarketplaceDbContext dbContext,
        ILlmProvider llmProvider,
        IDocumentGenerator documentGenerator,
        IEmbeddingService embeddingService,
        IOptions<AutoBiddingOptions> options,
        IAmazonS3 s3,
        TimeProvider timeProvider,
        ILogger<BiddingGenerationService> logger)
    {
        this.dbContext = dbContext;
        this.llmProvider = llmProvider;
        this.documentGenerator = documentGenerator;
        this.embeddingService = embeddingService;
        settings = options.Value;
        this.s3 = s3;
        this.timeProvider = timeProvider;
        this.logger = logger;
    }


    public async Task<BidGenerationResult> GenerateForTenderAsync(Guid tenderId, CancellationToken cancellationToken)
    {
        SeapTender? tender = await dbContext.SeapTenders.Include(item => item.Attachments)
            .SingleOrDefaultAsync(item => item.Id == tenderId, cancellationToken)
            ?? throw new KeyNotFoundException("Anunțul SEAP nu există.");

        string requirements = BuildRequirementsText(tender);
        float[] queryEmbedding = await embeddingService.EmbedAsync(requirements, cancellationToken);
        Vector vector = new(queryEmbedding);
        List<DocumentChunk> chunks = await dbContext.DocumentChunks
            .FromSqlInterpolated($"""
                SELECT * FROM "DocumentChunks"
                ORDER BY "Embedding" <=> {vector}
                LIMIT {Math.Max(1, settings.TopChunks)}
                """)
            .ToListAsync(cancellationToken);
        string vaultContext = string.Join("\n\n", chunks.Select(chunk => chunk.Content));

        string userPrompt =
            $"Cerințele anunțului:\n{requirements}\n\n" +
            $"Date despre firmă (top {chunks.Count} fragmente relevante din CompanyVault):\n{vaultContext}";
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

    private async Task<string> UploadDocumentAsync(Guid tenderId, GeneratedDocument document, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.DocumentsBucket))
            return $"memory://bids/{tenderId}/{document.FileName}";
        using MemoryStream stream = new(document.Content);
        await s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = settings.DocumentsBucket,
            Key = $"bids/{tenderId}/{document.FileName}",
            InputStream = stream,
            ContentType = document.ContentType
        }, cancellationToken);
        return $"s3://{settings.DocumentsBucket}/bids/{tenderId}/{document.FileName}";
    }

    [LoggerMessage(EventId = 5302, Level = LogLevel.Information, Message = "Auto-bid generated for SEAP tender {TenderId} (shadow mode: {ShadowMode}).")]
    private static partial void LogBidGenerated(ILogger logger, Guid tenderId, bool shadowMode);
}

