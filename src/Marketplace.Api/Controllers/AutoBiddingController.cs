using Marketplace.Application.AiIntake;
using Marketplace.Application.Bidding;
using Marketplace.Application.Billing;
using Marketplace.Application.Companies;
using Marketplace.Application.Seap;
using Marketplace.Domain.Seap;
using Marketplace.Infrastructure;
using Marketplace.Infrastructure.AutoBidding;
using Marketplace.Infrastructure.Bidding;
using Marketplace.Infrastructure.Billing;
using Marketplace.Infrastructure.Identity;
using Marketplace.Infrastructure.Persistence;
using Marketplace.Infrastructure.Seap;
using Marketplace.Infrastructure.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace Marketplace.Api.Controllers;

[ApiController]
[Route("api/v1/seap")]
[Authorize]
public sealed partial class AutoBiddingController(
    IBiddingGenerationService biddingService,
    IStripeGateway stripeGateway,
    ICompanyAuthorizationService companyAuthorizationService,
    UserManager<ApplicationUser> userManager,
    IOptions<AutoBiddingOptions> autoBiddingOptions,
    IOptionsMonitor<FeaturesOptions> features,
    ISeapIngestionService seapIngestionService,
    ISeapApiClient seapApiClient,
    ISeapSummaryGenerator summaryGenerator,
    ISeapAttachmentTextReader attachmentTextReader,
    ISeapOriginalDocumentStore originalDocumentStore,
    MarketplaceDbContext dbContext,
    IStorageService storage,
    IPipelineProgressStore progressStore,
    ILogger<AutoBiddingController> logger) : ControllerBase
{
    // MapInboundClaims (default) remaps the JWT "sub" claim to ClaimTypes.NameIdentifier, so read both.
    private Guid UserId => Guid.TryParse(
        User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier),
        out Guid id) ? id : Guid.Empty;

    private decimal ToEur(decimal? estimatedValueRon) =>
        estimatedValueRon is null
            ? 0m
            : estimatedValueRon.Value / (autoBiddingOptions.Value.RonToEurRate > 0 ? autoBiddingOptions.Value.RonToEurRate : 5m);

    [HttpGet("tenders")]
    public async Task<IActionResult> ListTenders(
        [FromQuery] Guid[]? categoryIds, [FromQuery] int? radiusKm, [FromQuery] string? county,
        [FromQuery] string[]? cpvPrefixes, [FromQuery] string[]? counties, [FromQuery] SeapTenderStatus? status,
        CancellationToken cancellationToken,
        [FromQuery] bool search = false, [FromQuery] DateOnly? from = null, [FromQuery] DateOnly? to = null)
    {
        // „Caută în SEAP": take the active parameters, fetch matching notices straight from the live
        // e-licitatie.ro search (optionally within the publication-date window), import them, then return
        // the local filtered list (which now includes the freshly imported tenders).
        if (search)
            await seapIngestionService.SearchAndImportAsync(cpvPrefixes, counties, from, to, cancellationToken);
        return Ok(await biddingService.ListTendersAsync(categoryIds, radiusKm, county, cancellationToken, cpvPrefixes, counties, status));
    }

    /// <summary>
    /// „Direct hit" resolve by SEAP notice number (SCN…/CN…). Local first: a document-grounded local tender
    /// is returned as ready (HTTP 200, full detail DTO). A local row that is not ready yet — or a notice that
    /// only exists live — gets a persistent ingestion run: metadata + the numeric NoticeId are saved (expired
    /// notices as <see cref="SeapImportMode.DirectHistoricalLookup"/>, never an active opportunity) and the
    /// endpoint answers HTTP 202 with only <c>tenderId</c>/<c>runId</c>/<c>status</c>. Documents and the LLM
    /// analysis are executed by the background ingestion worker (separate delivery), not inside this request.
    /// 404 only when SEAP answered and has no exact match; 502 when the platform is unreachable/erroring.
    /// </summary>
    [HttpPost("tenders/resolve")]
    [EnableRateLimiting("seap-resolve")]
    public async Task<IActionResult> ResolveTender(ResolveSeapTenderRequest request, CancellationToken cancellationToken)
    {
        if (request is null || !SeapNoticeNumber.TryNormalize(request.NoticeNo, out string noticeNo))
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["noticeNo"] = ["Numărul anunțului lipsește sau are un format neacceptat (ex. SCN1179200 / CN1010518)."]
            })
            { Title = "Cerere invalidă." });

        // Rollout gate: the Direct Hit surface must never create ingestion runs nobody processes. It is only
        // available when BOTH the Direct Hit surface AND the ingestion worker are enabled — otherwise resolve
        // answers 503 explicitly and no tender/run is created or touched.
        if (!features.CurrentValue.SeapDirectHitEnabled || !features.CurrentValue.SeapIngestionWorkerEnabled)
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "SEAP Direct Hit este indisponibil momentan.",
                detail: "Importul direct de anunțuri SEAP este dezactivat temporar. Încearcă din nou mai târziu.");

        try
        {
            SeapResolveResult? result = await seapIngestionService.ResolveNoticeByNumberAsync(noticeNo, cancellationToken);
            if (result is null)
                return NotFound(new
                {
                    message = $"Anunțul {noticeNo} nu a fost găsit pe SEAP.",
                    detail = $"Anunțul {noticeNo} nu a fost găsit pe SEAP. Verifică numărul și încearcă din nou."
                });

            // Unified envelope: the same shape for HTTP 200 (Ready/Partial — tender present) and 202
            // (processing — tender null). The SPA only needs tenderId/runId/status while work runs.
            if (result.ReadyTender is not null)
            {
                return Ok(new
                {
                    tenderId = result.TenderId,
                    runId = (Guid?)null,
                    noticeNo = result.NoticeNo,
                    status = result.Status,
                    tender = result.ReadyTender
                });
            }
            Response.Headers["Location"] = $"/api/v1/seap/tenders/{result.TenderId}/ingestion-status";
            Response.Headers["Retry-After"] = "30"; // best-effort ETA for the next worker tick
            return Accepted(new
            {
                tenderId = result.TenderId,
                runId = result.RunId,
                noticeNo = result.NoticeNo,
                status = result.Status,
                tender = (SeapTenderDetailsDto?)null,
                purpose = result.Purpose,
                message = result.Purpose == SeapIngestionRunPurpose.DocumentImport.ToString()
                    ? "Anunțul a fost găsit. Descărcăm și stocăm durabil documentele originale în dosarul anunțului (procesare în fundal); verifică statusul în scurt timp."
                    : "Anunțul a fost găsit și metadatele au fost importate. Procesarea în fundal (documente + analiză AI) a fost înregistrată; verifică statusul de ingestie în scurt timp."
            });
        }
        catch (SeapSourceUnavailableException exception)
        {
            LogSeapLookupUnavailable(logger, noticeNo, exception);
            return Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "SEAP indisponibil",
                detail: "Platforma SEAP nu a putut fi interogată în acest moment (acces restricționat sau serviciu întrerupt). Încearcă din nou mai târziu.");
        }
        catch (SeapResolveLimitReachedException exception)
        {
            Response.Headers["Retry-After"] = "30"; // global cap window — poll back after the worker drains runs
            return Problem(
                statusCode: StatusCodes.Status429TooManyRequests,
                title: "Prea multe procesări",
                detail: exception.Message);
        }
    }

    /// <summary>Persistent ingestion state of a tender — the SPA polls this while a resolve run is active.</summary>
    [HttpGet("tenders/{tenderId:guid}/ingestion-status")]
    public async Task<IActionResult> GetIngestionStatus(Guid tenderId, CancellationToken cancellationToken)
    {
        SeapIngestionStatusDto? status = await seapIngestionService.GetIngestionStatusAsync(tenderId, cancellationToken);
        return status is null ? NotFound() : Ok(status);
    }

    [HttpGet("tenders/{tenderId:guid}")]
    public async Task<IActionResult> GetTender(Guid tenderId, CancellationToken cancellationToken)
    {
        SeapTender? tender = await biddingService.GetTenderAsync(tenderId, cancellationToken);
        return tender is null ? NotFound() : Ok(SeapTenderDetailsDto.From(tender, DateTimeOffset.UtcNow));
    }

    /// <summary>
    /// Generates the „Sinteză AI" extraction on demand for a tender that does not have one yet (legacy
    /// tenders imported before the summarizer existed, or tenders whose import-time generation failed).
    /// Idempotent: when a summary already exists it is returned without an LLM call. A failed generation
    /// returns 503 with a friendly detail so the SPA shows a recoverable error instead of endless loading.
    /// </summary>
    [HttpPost("tenders/{tenderId:guid}/summary")]
    public async Task<IActionResult> GenerateSummary(Guid tenderId, CancellationToken cancellationToken)
    {
        SeapTender? tender = await dbContext.SeapTenders
            .Include(item => item.Attachments)
            .Include(item => item.Category)
            .SingleOrDefaultAsync(item => item.Id == tenderId, cancellationToken);
        if (tender is null) return NotFound();

        bool hasSummary = tender.SummaryPoints.Count > 0;
        bool canResolveDocuments = tender.NoticeId is not null || tender.Attachments.Count > 0;
        // A summary that was already grounded in the published document text — or that can never be enriched
        // (no numeric notice id / documents) — is returned as-is: idempotent, no LLM call.
        if (hasSummary && (tender.SummaryGroundedInDocuments || !canResolveDocuments))
            return Ok(SeapTenderDetailsDto.From(tender, DateTimeOffset.UtcNow));

        (string? attachmentText, string? documentNote) = await ResolveDocumentsForSynthesisAsync(tender, cancellationToken);

        // A metadata-only summary that could not be enriched right now (SEAP unreachable, no published
        // document or an unreadable/scanned file): keep the existing bullets and tell the user why.
        if (hasSummary && attachmentText is null)
        {
            SeapTenderDetailsDto unchanged = SeapTenderDetailsDto.From(tender, DateTimeOffset.UtcNow);
            return Ok(documentNote is null ? unchanged : unchanged with { SummaryDocumentNote = documentNote });
        }

        SeapAiExtraction? extraction = await summaryGenerator.GenerateAsync(tender, attachmentText, cancellationToken);
        if (extraction is null)
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Sinteza indisponibilă",
                detail: "Sinteza AI nu a putut fi generată în acest moment. Încearcă din nou mai târziu.");

        tender.ApplyAiExtraction(extraction, DateTimeOffset.UtcNow, groundedInDocuments: attachmentText is not null);
        await dbContext.SaveChangesAsync(cancellationToken);
        SeapTenderDetailsDto dto = SeapTenderDetailsDto.From(tender, DateTimeOffset.UtcNow);
        return Ok(documentNote is null ? dto : dto with { SummaryDocumentNote = documentNote });
    }
    /// <summary>
    /// Resolves the published documents (Fișa de date / Caiet de sarcini) for a tender that has none and
    /// reads their readable text, so the on-demand synthesis is grounded in the real document content.
    /// Returns the attachment text to feed the LLM plus a user-facing note when the synthesis had to fall
    /// back to the notice metadata (no document could be downloaded or read automatically).
    /// </summary>
    private async Task<(string? AttachmentText, string? DocumentNote)> ResolveDocumentsForSynthesisAsync(
        SeapTender tender, CancellationToken cancellationToken)
    {
        bool attemptedFetch = false;
        if (tender.Attachments.Count == 0 && tender.NoticeId is int noticeId)
        {
            attemptedFetch = true;
            try
            {
                IReadOnlyCollection<SeapNoticeDocumentDto> documents = await seapApiClient.FetchNoticeDocumentsAsync(
                    noticeId, tender.SysNoticeTypeId ?? 2, cancellationToken);
                DateTimeOffset now = DateTimeOffset.UtcNow;
                foreach (SeapNoticeDocumentDto document in documents)
                {
                    if (tender.Attachments.Any(attachment =>
                            string.Equals(attachment.FileName, document.FileName, StringComparison.OrdinalIgnoreCase)))
                        continue;
                    dbContext.Add(SeapTenderAttachment.Create(tender.Id, document.FileName, now,
                        sourceUrl: document.DownloadUrl, sourceDocumentId: document.Code, sourceSection: document.Section));
                }
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (Exception exception)
            {
                LogDocumentsResolveFailed(logger, tender.Id, exception);
            }
        }

        if (tender.Attachments.Count == 0)
        {
            string? note = attemptedFetch
                ? "Documentele publicate (Fișa de date, Caietul de sarcini) nu au putut fi descărcate automat de pe SEAP. Sinteza s-a generat doar din metadatele anunțului."
                : null;
            return (null, note);
        }

        SeapAttachmentReadResult read = await attachmentTextReader.ReadPublishedTextAsync(tender.Attachments, cancellationToken);
        return read.Text is null
            ? (null, "Fișa de date / Caietul de sarcini nu au putut fi citite automat (document scanat sau format neacceptat). Sinteza s-a generat doar din metadatele anunțului.")
            : (read.Text, null);
    }


    /// <summary>
    /// Resolves the published documents (Fișa de date, Caiet de sarcini, formulare) on demand for a tender
    /// that has none yet — the notice-search payload does not carry them and the on-demand search may not
    /// re-find an already-imported tender. Idempotent: an existing attachment list is returned without a
    /// fetch. Tenders without a persisted numeric notice id (pre-document-feature imports) return as-is —
    /// their id is backfilled when the on-demand search re-finds them. A failed fetch returns 503 with a
    /// friendly detail so the SPA shows a recoverable state.
    /// </summary>
    [HttpPost("tenders/{tenderId:guid}/documents")]
    public async Task<IActionResult> RefreshPublishedDocuments(Guid tenderId, CancellationToken cancellationToken)
    {
        SeapTender? tender = await dbContext.SeapTenders
            .Include(item => item.Attachments)
            .Include(item => item.Category)
            .SingleOrDefaultAsync(item => item.Id == tenderId, cancellationToken);
        if (tender is null) return NotFound();

        if (tender.Attachments.Count > 0 || tender.NoticeId is not int noticeId)
            return Ok(SeapTenderDetailsDto.From(tender, DateTimeOffset.UtcNow));

        try
        {
            IReadOnlyCollection<SeapNoticeDocumentDto> documents =
                await seapApiClient.FetchNoticeDocumentsAsync(noticeId, tender.SysNoticeTypeId ?? 2, cancellationToken);
            DateTimeOffset now = DateTimeOffset.UtcNow;
            foreach (SeapNoticeDocumentDto document in documents)
            {
                if (tender.Attachments.Any(attachment =>
                        string.Equals(attachment.FileName, document.FileName, StringComparison.OrdinalIgnoreCase)))
                    continue;
                dbContext.Add(SeapTenderAttachment.Create(tender.Id, document.FileName, now,
                    sourceUrl: document.DownloadUrl, sourceDocumentId: document.Code, sourceSection: document.Section));
            }
            await dbContext.SaveChangesAsync(cancellationToken);
            return Ok(SeapTenderDetailsDto.From(tender, DateTimeOffset.UtcNow));
        }
        catch (Exception exception)
        {
            LogDocumentsResolveFailed(logger, tenderId, exception);
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Documente indisponibile",
                detail: "Documentele anunțului nu au putut fi încărcate în acest moment. Încearcă din nou mai târziu.");
        }
    }

    /// <summary>
    /// Streams one of the original files published by the contracting authority (Fișa de date, Caiet de
    /// sarcini, clarificări) for browsing. The persisted local copy (browsing copy or dossier original) is
    /// resolved server-side; a row that only carries the remote e-licitatie.ro reference is proxied from the
    /// platform on demand (the file endpoint requires a same-origin Referer, set by SeapApiClient). The
    /// attachment is verified to belong to the tender so a user cannot read other tenders' files (IDOR guard).
    /// </summary>
    [HttpGet("tenders/{tenderId:guid}/attachments/{attachmentId:guid}")]
    public async Task<IActionResult> DownloadAttachment(
        Guid tenderId, Guid attachmentId, CancellationToken cancellationToken)
    {
        SeapTender? tender = await biddingService.GetTenderAsync(tenderId, cancellationToken);
        SeapTenderAttachment? attachment = tender?.Attachments.FirstOrDefault(item => item.Id == attachmentId);
        if (attachment is null) return NotFound();

        // A locally persisted copy (browsing copy or dossier original) streams from object storage.
        if (!string.IsNullOrWhiteSpace(attachment.StorageReference))
        {
            StorageObject? stored = await storage.OpenReadAsync(attachment.StorageReference, cancellationToken);
            return stored is null
                ? NotFound()
                : File(stored.Content, stored.ContentType, attachment.FileName);
        }

        // Rows that only carry the SEAP source URL (no copy persisted yet) are streamed from the platform on
        // demand (the file endpoint requires a same-origin Referer, set by SeapApiClient).
        if (!string.IsNullOrWhiteSpace(attachment.SourceUrl))
        {
            try
            {
                Stream remote = await seapApiClient.DownloadAttachmentAsync(attachment.SourceUrl, cancellationToken);
                return File(remote, SeapAttachmentStorage.DetectContentType(attachment.FileName), attachment.FileName);
            }
            catch (Exception)
            {
                return Problem(
                    statusCode: StatusCodes.Status502BadGateway,
                    title: "Document indisponibil",
                    detail: "Documentul nu este disponibil momentan pe platforma SEAP. Încearcă din nou mai târziu.");
            }
        }

        return NotFound();
    }

    /// <summary>
    /// Controlled dossier download: streams the EXACT bytes of a durably stored original document
    /// (a <see cref="SeapDocumentDownloadStatus.Downloaded"/> attachment) from the Direct Hit dossier store,
    /// under the original file name (e.g. <c>DUAE_CERERE_1.xml</c>, <c>Fisa de date.pdf.p7s</c>) and the stored
    /// content type. The bytes are never transformed — a signed <c>.p7s</c> is served as-is, never unwrapped.
    /// The attachment must belong to the tender (IDOR guard) and must be durably stored; rows that only keep a
    /// remote SEAP reference are served by the browsing route instead and return 404 here.
    /// </summary>
    [HttpGet("tenders/{tenderId:guid}/documents/{attachmentId:guid}/download")]
    public async Task<IActionResult> DownloadDossierOriginal(
        Guid tenderId, Guid attachmentId, CancellationToken cancellationToken)
    {
        SeapTender? tender = await biddingService.GetTenderAsync(tenderId, cancellationToken);
        SeapTenderAttachment? attachment = tender?.Attachments.FirstOrDefault(item => item.Id == attachmentId);
        if (attachment is null) return NotFound();
        if (attachment.DownloadStatus != SeapDocumentDownloadStatus.Downloaded
            || string.IsNullOrWhiteSpace(attachment.StorageReference))
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Original indisponibil",
                detail: "Documentul original nu a fost încă stocat durabil în dosarul anunțului.");
        }

        SeapOriginalDocumentContent? original =
            await originalDocumentStore.OpenOriginalAsync(attachment.StorageReference, cancellationToken);
        return original is null
            ? NotFound()
            // Serve the original file name from the attachment row (never from the storage key) and the stored
            // content type; the bytes are streamed untouched — .p7s files are delivered signed, never unpacked.
            : File(original.Content, original.ContentType, attachment.FileName);
    }

    /// <summary>Fair-pricing preview shown before starting the generation (Task 3: 1 free generation, then
    /// dynamic tiers on the tender's estimated value).</summary>
    [HttpGet("tenders/{tenderId:guid}/pricing")]
    public async Task<IActionResult> GetPricing(Guid tenderId, CancellationToken cancellationToken)
    {
        SeapTender? tender = await biddingService.GetTenderAsync(tenderId, cancellationToken);
        if (tender is null) return NotFound();
        ApplicationUser? user = await userManager.FindByIdAsync(UserId.ToString());
        int freeGenerationsRemaining = user?.FreeGenerationsRemaining ?? 0;
        decimal estimatedValueEur = ToEur(tender.EstimatedValue);
        int priceEur = PricingCalculator.ComputePriceEur(estimatedValueEur);
        return Ok(new
        {
            estimatedValueEur = Math.Round(estimatedValueEur, 2),
            priceEur,
            freeGenerationsRemaining,
            shadowMode = autoBiddingOptions.Value.IsShadowMode
        });
    }

    /// <summary>Starts auto-bid: in Shadow Mode it generates silently without charging; otherwise the first
    /// generation per account is free (launch offer), and any further generation creates a Stripe Checkout
    /// Session with dynamic PriceData from the estimated-value tiers (EUR). The launch offer is consumed in
    /// Shadow Mode too, so the pricing preview reflects the real remaining entitlement while rehearsing.</summary>
    [HttpPost("tenders/{tenderId:guid}/auto-bid")]
    public async Task<IActionResult> StartAutoBid(
        Guid tenderId, [FromBody] StartAutoBidRequest? request, CancellationToken cancellationToken)
    {
        if (!features.CurrentValue.AutoBiddingEnabled)
            return NotFound();
        SeapTender? tender = await biddingService.GetTenderAsync(tenderId, cancellationToken);
        if (tender is null) return NotFound();
        Guid? companyId = request?.CompanyId;
        // The SEAP qualification vault is per-company; a caller that cannot represent the company gets 404
        // (no existence leak). A null company is allowed — the generator falls back to placeholders.
        if (companyId is Guid representedCompany &&
            !await companyAuthorizationService.CanRepresentCompanyAsync(UserId, representedCompany, cancellationToken))
            return NotFound();
        try
        {
            if (autoBiddingOptions.Value.IsShadowMode)
            {
                ApplicationUser? shadowUser = await userManager.FindByIdAsync(UserId.ToString());
                bool freeGeneration = shadowUser is not null && shadowUser.FreeGenerationsRemaining > 0;
                BidGenerationResult result = await biddingService.GenerateForTenderAsync(
                    companyId, tenderId, cancellationToken, request?.ProgressRequestId);
                // Consume the launch offer only after a successful generation, so a failed attempt (Claude
                // down, storage error) does not burn the free entitlement.
                if (freeGeneration)
                {
                    shadowUser!.FreeGenerationsRemaining -= 1;
                    await userManager.UpdateAsync(shadowUser);
                }
                ReportCompleted(request?.ProgressRequestId);
                return Ok(new { ready = true, shadowMode = true, freeGeneration, result });
            }

            ApplicationUser? user = await userManager.FindByIdAsync(UserId.ToString());
            if (user is not null && user.FreeGenerationsRemaining > 0)
            {
                BidGenerationResult result = await biddingService.GenerateForTenderAsync(
                    companyId, tenderId, cancellationToken, request?.ProgressRequestId);
                user.FreeGenerationsRemaining -= 1;
                await userManager.UpdateAsync(user);
                ReportCompleted(request?.ProgressRequestId);
                return Ok(new { ready = true, shadowMode = false, freeGeneration = true, result });
            }

            int priceEur = PricingCalculator.ComputePriceEur(ToEur(tender.EstimatedValue));
            StripeCheckoutSession session = await stripeGateway.CreateCheckoutSessionAsync(
                new(tender.Id, priceEur, tender.Title, companyId), cancellationToken);
            return Ok(new { ready = false, shadowMode = false, checkoutUrl = session.CheckoutUrl, sessionId = session.SessionId, priceEur });
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            if (request?.ProgressRequestId is Guid failedRequestId)
                progressStore.Report(failedRequestId, PipelineStage.Failed, exception.Message);
            throw;
        }
    }

    /// <summary>Current stage of a running SEAP auto-bid generation (the SPA polls this while the POST
    /// /auto-bid request is in flight to show real progress instead of an endless spinner).</summary>
    [HttpGet("tenders/{tenderId:guid}/auto-bid/progress/{requestId:guid}")]
    public IActionResult GetAutoBidProgress(Guid requestId)
    {
        PipelineProgress? progress = progressStore.Find(requestId);
        return progress is null ? NotFound() : Ok(progress);
    }

    private void ReportCompleted(Guid? requestId)
    {
        if (requestId is Guid completedId)
            progressStore.Report(completedId, PipelineStage.Completed, "Propunerea a fost generată.");
    }

    [HttpGet("tenders/{tenderId:guid}/bid-status")]
    public async Task<IActionResult> GetBidStatus(Guid tenderId, CancellationToken cancellationToken)
    {
        SeapTender? tender = await biddingService.GetTenderAsync(tenderId, cancellationToken);
        if (tender is null) return NotFound();
        return Ok(new
        {
            status = tender.Status.ToString(),
            tender.ProposalTitle,
            tender.TechnicalProposalUrl,
            tender.BillOfQuantitiesUrl
        });
    }

    /// <summary>
    /// Streams one of the auto-generated proposal files (technical .docx or bill of quantities .xlsx)
    /// for a tender. The persisted storage reference (<c>local://…</c> or <c>s3://…</c>) is resolved
    /// server-side so browsers never see a non-navigable scheme.
    /// </summary>
    [HttpGet("tenders/{tenderId:guid}/bid-file")]
    public async Task<IActionResult> DownloadBidFile(
        Guid tenderId, [FromQuery] GeneratedBidFileKind kind, CancellationToken cancellationToken)
    {
        SeapTender? tender = await biddingService.GetTenderAsync(tenderId, cancellationToken);
        if (tender is null) return NotFound();
        string? reference = kind switch
        {
            GeneratedBidFileKind.TechnicalProposal => tender.TechnicalProposalUrl,
            _ => tender.BillOfQuantitiesUrl
        };
        if (string.IsNullOrWhiteSpace(reference)) return NotFound();
        StorageObject? stored = await storage.OpenReadAsync(reference, cancellationToken);
        return stored is null
            ? NotFound()
            : File(stored.Content, stored.ContentType, StorageObject.FileNameFromReference(reference));
    }

    [AllowAnonymous]
    [HttpPost("webhooks/stripe")]
    public async Task<IActionResult> StripeWebhook(
        [FromHeader(Name = "Stripe-Signature")] string signature,
        CancellationToken cancellationToken)
    {
        using StreamReader reader = new(Request.Body);
        string payload = await reader.ReadToEndAsync(cancellationToken);
        StripeCompletedCheckout? checkout = await stripeGateway.ParseCompletedCheckoutEventAsync(payload, signature);
        if (checkout is null) return BadRequest();
        if (features.CurrentValue.AutoBiddingEnabled)
        {
            _ = Task.Run(async () =>
            {
                try { await biddingService.GenerateForTenderAsync(checkout.CompanyId, checkout.TenderId, CancellationToken.None); }
                catch (Exception exception) { LogWebhookGenerationFailed(logger, checkout.TenderId, exception); }
            }, CancellationToken.None);
        }
        return Ok();
    }

    [LoggerMessage(EventId = 6001, Level = LogLevel.Error, Message = "Auto-bid generation after checkout failed for tender {TenderId}.")]
    private static partial void LogWebhookGenerationFailed(ILogger logger, Guid tenderId, Exception exception);
    [LoggerMessage(EventId = 6002, Level = LogLevel.Warning, Message = "Published documents could not be resolved for tender {TenderId}.")]
    private static partial void LogDocumentsResolveFailed(ILogger logger, Guid tenderId, Exception exception);
    [LoggerMessage(EventId = 6003, Level = LogLevel.Warning, Message = "Direct SEAP notice-number lookup for {NoticeNo} failed because the platform is unavailable.")]
    private static partial void LogSeapLookupUnavailable(ILogger logger, string noticeNo, Exception exception);
}

/// <summary>Body of POST /auto-bid; <c>companyId</c> identifies the company whose SEAP qualification vault
/// feeds the proposal (validated with CanRepresentCompany; null falls back to placeholders), and the
/// optional <c>progressRequestId</c> lets the SPA poll real per-stage progress (Generating → Finalizing →
/// Completed/Failed) while the request is in flight.</summary>
public sealed record StartAutoBidRequest(Guid? ProgressRequestId, Guid? CompanyId = null);

/// <summary>Body of POST /api/v1/seap/tenders/resolve — a notice-number direct-hit resolve (SCN…/CN…).</summary>
public sealed record ResolveSeapTenderRequest(string NoticeNo);
