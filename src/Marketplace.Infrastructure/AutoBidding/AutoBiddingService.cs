using System.Text.Json;
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
        "\"quantity\": number, \"unitPrice\": number, \"total\": number}], " +
        "\"financialSummary\": [{\"label\": string, \"value\": number, \"note\": string}]}. " +
        "Lista de cantități trebuie să conțină TOATE punctele numerotate din caiet, fără excepție. " +
        "financialSummary: copiază valorile din recapitulația financiară a caietului (ex: TOTAL 1 Cheltuieli directe, " +
        "T2, T3, TOTAL GENERAL fără TVA, TVA, TOTAL GENERAL cu TVA, T4 = T3 + beneficiu); dacă nu există, returnează []. " +
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
    private readonly IPipelineProgressStore? progressStore;
    private Guid? _activeProgressRequestId;

    public AutoBiddingService(
        MarketplaceDbContext dbContext,
        IStorageService storage,
        IDocumentExtractionService extractionService,
        ILlmProvider llmProvider,
        IDocumentGenerator documentGenerator,
        IOptions<AutoBiddingOptions> options,
        IOptions<LocalStorageOptions> localStorage,
        TimeProvider timeProvider,
        ILogger<AutoBiddingService> logger,
        IPipelineProgressStore? progressStore = null)
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
        this.progressStore = progressStore;
    }

    public async Task<SpecificationBidResult> ProcessSpecificationAsync(
        Guid companyId, string fileName, Stream content, Guid actorId, CancellationToken cancellationToken,
        Guid? progressRequestId = null)
    {
        _activeProgressRequestId = progressRequestId;
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
        // Discovery through the collection navigation can classify the new document as Modified instead of
        // Added when the vault is already tracked; mark it explicitly so the save inserts rather than updates.
        if (dbContext.Entry(document).State != EntityState.Added) dbContext.VaultDocuments.Add(document);
        await dbContext.SaveChangesAsync(cancellationToken);
        LogSpecificationSaved(logger, document.Id);

        // Step 2 — extract text/tables as Markdown via Azure Document Intelligence.
        LogExtractionStarted(logger, fileName);
        ReportProgress(PipelineStage.Extracting, "Se extrag tabelele și textul din Caietul de Sarcini…");
        buffer.Position = 0;
        DocumentExtractionResult extraction = await extractionService.ExtractAsync(buffer, fileName, cancellationToken);
        string markdown = MarkdownConverter.ToMarkdown(extraction);
        LogExtractionCompleted(logger, document.Id, extraction.Pages.Count, markdown.Length);
        EnsureSufficientMarkdown(document.Id, fileName, markdown);
        document.SetExtractedMarkdown(markdown);
        document.MarkExtracted(extraction.Pages.Count);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Steps 3–4 — generate and store the proposal, then persist the outcome.
        return await GenerateProposalFromMarkdownAsync(document, markdown, cancellationToken);
    }

    public async Task<SpecificationBidResult> GenerateFromSpecificationAsync(
        Guid companyId, Guid documentId, CancellationToken cancellationToken, Guid? progressRequestId = null)
    {
        _activeProgressRequestId = progressRequestId;
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
        EnsureSufficientMarkdown(document.Id, document.FileName, document.ExtractedMarkdown);

        return await GenerateProposalFromMarkdownAsync(document, document.ExtractedMarkdown, cancellationToken);
    }

    private async Task<SpecificationBidResult> GenerateProposalFromMarkdownAsync(
        VaultDocument document, string markdown, CancellationToken cancellationToken)
    {
        // LLM proposal generation (Claude via ILlmProvider; deterministic mock in Development).
        string userPrompt = $"Caiet de sarcini (extras automat, format Markdown):\n\n{markdown}";
        LogGenerationStarted(logger, document.Id, settings.Model);
        LogLlmRequest(logger, document.Id, SpecificationPrompt.Length, userPrompt.Length, settings.Model);
        ReportProgress(PipelineStage.Generating, "Claude redactează propunerea tehnică…");
        string llmJson = await llmProvider.CompleteAsync(SpecificationPrompt, userPrompt, cancellationToken);
        LogLlmRawResponse(logger, document.Id, llmJson.Length, llmJson);

        // Structural parsing failure (missing sections/quantities or malformed JSON) must never result in
        // silently empty documents: log the raw response and fail the pipeline.
        BiddingProposal proposal;
        try
        {
            proposal = BiddingJsonParser.Parse(llmJson);
        }
        catch (Exception exception) when (exception is InvalidOperationException or JsonException)
        {
            LogLlmParseFailed(logger, document.Id, exception.Message, llmJson);
            throw;
        }
        LogProposalParsed(logger, document.Id, proposal.Title, proposal.Sections.Count, proposal.BillOfQuantities.Count);
        LogProposalGenerated(logger, document.Id, proposal.Title);

        // Generate and store the technical proposal (.docx) and bill of quantities (.xlsx).
        ReportProgress(PipelineStage.Finalizing, "Se generează documentele (.docx / .xlsx)…");
        GeneratedDocument technical = await documentGenerator.GenerateTechnicalProposalAsync(proposal, cancellationToken);
        GeneratedDocument boq = await documentGenerator.GenerateBillOfQuantitiesAsync(proposal, cancellationToken);
        LogDocumentsGenerated(logger, document.Id, technical.FileName, technical.Content.Length, boq.FileName, boq.Content.Length);
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

    /// <summary>
    /// Guards the pipeline against OCR/PDFs that yield (almost) no text: generating an empty proposal
    /// from a few characters is a silent data-loss failure, so fail explicitly with a clear error.
    /// </summary>
    private void EnsureSufficientMarkdown(Guid documentId, string fileName, string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown) || markdown.Length < 200)
        {
            LogInsufficientOcrText(logger, documentId, fileName, markdown.Length);
            throw new AutoBiddingPipelineException(422,
                "PDF-ul pare scanat ilizibil sau protejat (textul extras este insuficient). " +
                "Verifică dacă fișierul conține text selectabil; dacă este un scan, mărește rezoluția și încearcă din nou.");
        }
    }

    /// <summary>Publishes the current pipeline stage so the UI can show real per-step progress.</summary>
    private void ReportProgress(PipelineStage stage, string message)
    {
        if (progressStore is not null && _activeProgressRequestId is Guid requestId)
            progressStore.Report(requestId, stage, message);
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
    [LoggerMessage(EventId = 6110, Level = LogLevel.Error, Message = "[ERROR] Azure OCR returned insufficient text: {MarkdownLength} chars for document {DocumentId} ({FileName}).")]
    private static partial void LogInsufficientOcrText(ILogger logger, Guid documentId, string fileName, int markdownLength);
    [LoggerMessage(EventId = 6111, Level = LogLevel.Information, Message = "[AutoBid] LLM request for {DocumentId}: system {SystemPromptLength} chars, user {UserPromptLength} chars (model {Model}).")]
    private static partial void LogLlmRequest(ILogger logger, Guid documentId, int systemPromptLength, int userPromptLength, string model);
    [LoggerMessage(EventId = 6112, Level = LogLevel.Information, Message = "[AutoBid] LLM raw response for {DocumentId}: {ResponseLength} chars:\n{Response}")]
    private static partial void LogLlmRawResponse(ILogger logger, Guid documentId, int responseLength, string response);
    [LoggerMessage(EventId = 6113, Level = LogLevel.Error, Message = "[ERROR] LLM response for {DocumentId} failed structural parsing: {Reason}\nRaw response:\n{Response}")]
    private static partial void LogLlmParseFailed(ILogger logger, Guid documentId, string reason, string response);
    [LoggerMessage(EventId = 6114, Level = LogLevel.Information, Message = "[AutoBid] Proposal parsed for {DocumentId}: {Title} ({SectionCount} sections, {BoqLineCount} BOQ lines).")]
    private static partial void LogProposalParsed(ILogger logger, Guid documentId, string title, int sectionCount, int boqLineCount);
    [LoggerMessage(EventId = 6115, Level = LogLevel.Information, Message = "[AutoBid] Generated documents for {DocumentId}: {TechnicalFileName} ({TechnicalBytes} bytes), {BoqFileName} ({BoqBytes} bytes).")]
    private static partial void LogDocumentsGenerated(ILogger logger, Guid documentId, string technicalFileName, int technicalBytes, string boqFileName, int boqBytes);
}
