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

        if (tender.SummaryPoints.Count > 0)
            return Ok(SeapTenderDetailsDto.From(tender, DateTimeOffset.UtcNow));

        SeapAiExtraction? extraction = await summaryGenerator.GenerateAsync(tender, attachmentText: null, cancellationToken);
        if (extraction is null)
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Sinteza indisponibilă",
                detail: "Sinteza AI nu a putut fi generată în acest moment. Încearcă din nou mai târziu.");

        tender.ApplyAiExtraction(extraction, DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(SeapTenderDetailsDto.From(tender, DateTimeOffset.UtcNow));
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
                dbContext.Add(SeapTenderAttachment.Create(tender.Id, document.FileName, document.DownloadUrl, now));
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
    /// sarcini, clarificări). The persisted storage reference (<c>local://…</c> or <c>s3://…</c>) is resolved
    /// server-side and the response streams directly (no in-memory buffering); the attachment is verified to
    /// belong to the tender so a user cannot read other tenders' files (IDOR guard).
    /// </summary>
    [HttpGet("tenders/{tenderId:guid}/attachments/{attachmentId:guid}")]
    public async Task<IActionResult> DownloadAttachment(
        Guid tenderId, Guid attachmentId, CancellationToken cancellationToken)
    {
        SeapTender? tender = await biddingService.GetTenderAsync(tenderId, cancellationToken);
        SeapTenderAttachment? attachment = tender?.Attachments.FirstOrDefault(item => item.Id == attachmentId);
        if (attachment is null) return NotFound();

        // Published documents resolved from e-licitatie.ro are stored as remote references; stream them from
        // the platform on demand (the file endpoint requires a same-origin Referer, set by SeapApiClient).
        if (attachment.S3Url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                Stream remote = await seapApiClient.DownloadAttachmentAsync(attachment.S3Url, cancellationToken);
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

        StorageObject? stored = await storage.OpenReadAsync(attachment.S3Url, cancellationToken);
        return stored is null
            ? NotFound()
            : File(stored.Content, stored.ContentType, StorageObject.FileNameFromReference(attachment.S3Url));
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
}

/// <summary>Body of POST /auto-bid; <c>companyId</c> identifies the company whose SEAP qualification vault
/// feeds the proposal (validated with CanRepresentCompany; null falls back to placeholders), and the
/// optional <c>progressRequestId</c> lets the SPA poll real per-stage progress (Generating → Finalizing →
/// Completed/Failed) while the request is in flight.</summary>
public sealed record StartAutoBidRequest(Guid? ProgressRequestId, Guid? CompanyId = null);
