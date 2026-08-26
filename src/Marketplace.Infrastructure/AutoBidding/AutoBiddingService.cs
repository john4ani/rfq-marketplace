using Marketplace.Application.AutoBidding;
using Marketplace.Domain.Profiles;
using Marketplace.Domain.Vault;
using Marketplace.Infrastructure.Bidding;
using Marketplace.Infrastructure.Documents;
using Marketplace.Infrastructure.Persistence;
using Marketplace.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Marketplace.Infrastructure.AutoBidding;

/// <summary>
/// Auto-bidding pipeline for an uploaded Caiet de sarcini PDF:
///   1. save the PDF to object storage (Cloudflare R2 in prod, local disk in Development);
///   2. extract text/tables as Markdown via Azure Document Intelligence (prebuilt-layout);
///   3. persist the Markdown on the vault document;
///   4. send the Markdown through a clear prompt to the configured LLM (Claude) and parse a basic
///      technical proposal + bill of quantities, then store the generated .docx/.xlsx.
/// Every step is logged so the pipeline can be followed during a local test.
/// </summary>
public sealed partial class AutoBiddingService : IAutoBiddingService
{
    private const string SpecificationPrompt =
        "Ești un expert român în achiziții publice. Pe baza Caietului de Sarcini primit, " +
        "generează o propunere tehnică de bază completă și o listă de cantități. " +
        "Răspunde DOAR cu JSON valid, în limba română, fără text înainte sau după, cu structura: " +
        "{\"title\": string, \"technicalProposal\": {\"sections\": [{\"heading\": string, \"content\": string}]}, " +
        "\"billOfQuantities\": [{\"position\": int, \"description\": string, \"unit\": string, " +
        "\"quantity\": number, \"unitPrice\": number, \"total\": number}]}. " +
        "Nu inventa certificări, prețuri sau capacități care nu sunt susținute de Caietul de Sarcini.";

    private readonly MarketplaceDbContext dbContext;
    private readonly IStorageService storage;
    private readonly IDocumentExtractionService extractionService;
    private readonly ILlmProvider llmProvider;
    private readonly IDocumentGenerator documentGenerator;
    private readonly AutoBiddingOptions settings;
    private readonly bool useLocalStorage;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<AutoBiddingService> logger;

    public AutoBiddingService(
        MarketplaceDbContext dbContext,
        IStorageService storage,
        IDocumentExtractionService extractionService,
        ILlmProvider llmProvider,
        IDocumentGenerator documentGenerator,
        IOptions<AutoBiddingOptions> options,
        IOptions<LocalStorageOptions> localStorage,
        TimeProvider timeProvider,
        ILogger<AutoBiddingService> logger)
    {
        this.dbContext = dbContext;
        this.storage = storage;
        this.extractionService = extractionService;
        this.llmProvider = llmProvider;
        this.documentGenerator = documentGenerator;
        settings = options.Value;
        useLocalStorage = localStorage.Value.UseLocalStorage;
        this.timeProvider = timeProvider;
        this.logger = logger;
    }

    public async Task<SpecificationBidResult> ProcessSpecificationAsync(
        Guid companyId, string fileName, Stream content, Guid actorId, CancellationToken cancellationToken)
    {
        LogUploadStarted(logger, fileName);
        using MemoryStream buffer = new();
        await content.CopyToAsync(buffer, cancellationToken);

        // Step 1 — save the PDF to object storage (R2/local/memory fallback).
        string documentUrl = await UploadSpecificationAsync(companyId, fileName, buffer, cancellationToken);
        LogUploaded(logger, fileName, documentUrl);

        DateTimeOffset now = timeProvider.GetUtcNow();
        CompanyVault vault = await dbContext.CompanyVaults.Include(item => item.Documents)
            .SingleOrDefaultAsync(item => item.SupplierId == companyId, cancellationToken)
            ?? CompanyVault.Create(companyId, now);
        // CompanyVault.Create assigns a new Guid, so a fresh vault must be attached explicitly.
        if (dbContext.Entry(vault).State == EntityState.Detached) dbContext.CompanyVaults.Add(vault);

        VaultDocument document = vault.AddDocument(fileName, documentUrl, VaultDocumentType.Specification, actorId, now);
        await dbContext.SaveChangesAsync(cancellationToken);
        LogSpecificationSaved(logger, document.Id);

        // Step 2 — extract text/tables as Markdown via Azure Document Intelligence.
        LogExtractionStarted(logger, fileName);
        buffer.Position = 0;
        DocumentExtractionResult extraction = await extractionService.ExtractAsync(buffer, fileName, cancellationToken);
        string markdown = MarkdownConverter.ToMarkdown(extraction);
        document.SetExtractedMarkdown(markdown);
        document.MarkExtracted(extraction.Pages.Count);
        await dbContext.SaveChangesAsync(cancellationToken);
        LogExtractionCompleted(logger, document.Id, extraction.Pages.Count, markdown.Length);

        // Steps 3–4 — generate and store the proposal, then persist the outcome.
        return await GenerateProposalFromMarkdownAsync(document, markdown, cancellationToken);
    }

    public async Task<SpecificationBidResult> GenerateFromSpecificationAsync(
        Guid companyId, Guid documentId, CancellationToken cancellationToken)
    {
        CompanyVault vault = await dbContext.CompanyVaults
            .Include(item => item.Documents)
            .SingleOrDefaultAsync(item => item.SupplierId == companyId, cancellationToken)
            ?? throw new KeyNotFoundException("Vault-ul companiei nu există.");
        VaultDocument document = vault.Documents.SingleOrDefault(item => item.Id == documentId)
            ?? throw new KeyNotFoundException("Documentul Caiet de sarcini nu există.");
        if (document.DocumentType != VaultDocumentType.Specification)
            throw new InvalidOperationException("Documentul nu este un Caiet de sarcini.");
        if (string.IsNullOrWhiteSpace(document.ExtractedMarkdown))
            throw new InvalidOperationException("Markdown-ul nu a fost extras. Reîncarcă documentul pentru a rula pipeline-ul complet.");

        return await GenerateProposalFromMarkdownAsync(document, document.ExtractedMarkdown, cancellationToken);
    }

    private async Task<SpecificationBidResult> GenerateProposalFromMarkdownAsync(
        VaultDocument document, string markdown, CancellationToken cancellationToken)
    {
        // LLM proposal generation (Claude via ILlmProvider; deterministic mock in Development).
        LogGenerationStarted(logger, document.Id, settings.Model);
        string llmJson = await llmProvider.CompleteAsync(
            SpecificationPrompt,
            $"Caiet de sarcini (extras automat, format Markdown):\n\n{markdown}",
            cancellationToken);
        BiddingProposal proposal = BiddingJsonParser.Parse(llmJson);
        LogProposalGenerated(logger, document.Id, proposal.Title);

        // Generate and store the technical proposal (.docx) and bill of quantities (.xlsx).
        GeneratedDocument technical = await documentGenerator.GenerateTechnicalProposalAsync(proposal, cancellationToken);
        GeneratedDocument boq = await documentGenerator.GenerateBillOfQuantitiesAsync(proposal, cancellationToken);
        string technicalUrl = await UploadGeneratedDocumentAsync(document.Id, technical, cancellationToken);
        string boqUrl = await UploadGeneratedDocumentAsync(document.Id, boq, cancellationToken);
        LogDocumentsStored(logger, document.Id, technicalUrl, boqUrl);

        document.RecordGeneratedProposal(proposal.Title, technicalUrl, boqUrl);
        await dbContext.SaveChangesAsync(cancellationToken);
        LogPipelineCompleted(logger, document.Id, proposal.Title);

        return new(
            document.Id,
            document.FileName,
            document.S3Url,
            markdown,
            proposal.Title,
            technicalUrl,
            boqUrl,
            settings.IsShadowMode,
            timeProvider.GetUtcNow());
    }

    private async Task<string> UploadSpecificationAsync(Guid companyId, string fileName, Stream content, CancellationToken cancellationToken)
    {
        if (useLocalStorage)
            return await storage.UploadAsync("specifications", $"specifications/{companyId}", fileName, content, "application/pdf", cancellationToken);
        if (string.IsNullOrWhiteSpace(settings.DocumentsBucket))
            return $"memory://specifications/{companyId}/{fileName}";
        return await storage.UploadAsync(settings.DocumentsBucket, $"specifications/{companyId}", fileName, content, "application/pdf", cancellationToken);
    }

    private async Task<string> UploadGeneratedDocumentAsync(Guid documentId, GeneratedDocument generated, CancellationToken cancellationToken)
    {
        if (useLocalStorage)
            return await storage.UploadAsync("bids", $"bids/{documentId}", generated.FileName,
                new MemoryStream(generated.Content), generated.ContentType, cancellationToken);
        if (string.IsNullOrWhiteSpace(settings.DocumentsBucket))
            return $"memory://bids/{documentId}/{generated.FileName}";
        return await storage.UploadAsync(settings.DocumentsBucket, $"bids/{documentId}", generated.FileName,
            new MemoryStream(generated.Content), generated.ContentType, cancellationToken);
    }

    [LoggerMessage(EventId = 6101, Level = LogLevel.Information, Message = "[AutoBid] Upload Caiet de sarcini started: {FileName}.")]
    private static partial void LogUploadStarted(ILogger logger, string fileName);
    [LoggerMessage(EventId = 6102, Level = LogLevel.Information, Message = "[AutoBid] Caiet de sarcini {FileName} saved to storage: {Url}.")]
    private static partial void LogUploaded(ILogger logger, string fileName, string url);
    [LoggerMessage(EventId = 6103, Level = LogLevel.Information, Message = "[AutoBid] Specification document {DocumentId} persisted.")]
    private static partial void LogSpecificationSaved(ILogger logger, Guid documentId);
    [LoggerMessage(EventId = 6104, Level = LogLevel.Information, Message = "[AutoBid] Azure Document Intelligence extraction started for {FileName}.")]
    private static partial void LogExtractionStarted(ILogger logger, string fileName);
    [LoggerMessage(EventId = 6105, Level = LogLevel.Information, Message = "[AutoBid] Extraction completed for {DocumentId}: {PageCount} pages, {MarkdownLength} markdown chars.")]
    private static partial void LogExtractionCompleted(ILogger logger, Guid documentId, int pageCount, int markdownLength);
    [LoggerMessage(EventId = 6106, Level = LogLevel.Information, Message = "[AutoBid] LLM proposal generation started for {DocumentId} (model {Model}).")]
    private static partial void LogGenerationStarted(ILogger logger, Guid documentId, string model);
    [LoggerMessage(EventId = 6107, Level = LogLevel.Information, Message = "[AutoBid] Proposal generated for {DocumentId}: {Title}.")]
    private static partial void LogProposalGenerated(ILogger logger, Guid documentId, string title);
    [LoggerMessage(EventId = 6108, Level = LogLevel.Information, Message = "[AutoBid] Proposal documents stored for {DocumentId}: {TechnicalUrl} / {BoqUrl}.")]
    private static partial void LogDocumentsStored(ILogger logger, Guid documentId, string technicalUrl, string boqUrl);
    [LoggerMessage(EventId = 6109, Level = LogLevel.Information, Message = "[AutoBid] Pipeline completed for {DocumentId}: {Title}.")]
    private static partial void LogPipelineCompleted(ILogger logger, Guid documentId, string title);
}
