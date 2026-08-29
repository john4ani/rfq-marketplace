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
using Marketplace.Infrastructure.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
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
    UserManager<ApplicationUser> userManager,
    IOptions<AutoBiddingOptions> autoBiddingOptions,
    IOptionsMonitor<FeaturesOptions> features,
    ISeapIngestionService seapIngestionService,
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
        [FromQuery] string[]? cpvPrefixes, [FromQuery] string[]? counties,
        CancellationToken cancellationToken,
        [FromQuery] bool search = false, [FromQuery] DateOnly? from = null, [FromQuery] DateOnly? to = null)
    {
        // „Caută în SEAP": take the active parameters, fetch matching notices straight from the live
        // e-licitatie.ro search (optionally within the publication-date window), import them, then return
        // the local filtered list (which now includes the freshly imported tenders).
        if (search)
            await seapIngestionService.SearchAndImportAsync(cpvPrefixes, counties, from, to, cancellationToken);
        return Ok(await biddingService.ListTendersAsync(categoryIds, radiusKm, county, cancellationToken, cpvPrefixes, counties));
    }

    [HttpGet("tenders/{tenderId:guid}")]
    public async Task<IActionResult> GetTender(Guid tenderId, CancellationToken cancellationToken)
    {
        SeapTender? tender = await biddingService.GetTenderAsync(tenderId, cancellationToken);
        return tender is null ? NotFound() : Ok(tender);
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
        try
        {
            if (autoBiddingOptions.Value.IsShadowMode)
            {
                ApplicationUser? shadowUser = await userManager.FindByIdAsync(UserId.ToString());
                bool freeGeneration = shadowUser is not null && shadowUser.FreeGenerationsRemaining > 0;
                BidGenerationResult result = await biddingService.GenerateForTenderAsync(
                    tenderId, cancellationToken, request?.ProgressRequestId);
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
                    tenderId, cancellationToken, request?.ProgressRequestId);
                user.FreeGenerationsRemaining -= 1;
                await userManager.UpdateAsync(user);
                ReportCompleted(request?.ProgressRequestId);
                return Ok(new { ready = true, shadowMode = false, freeGeneration = true, result });
            }

            int priceEur = PricingCalculator.ComputePriceEur(ToEur(tender.EstimatedValue));
            StripeCheckoutSession session = await stripeGateway.CreateCheckoutSessionAsync(
                new(tender.Id, priceEur, tender.Title), cancellationToken);
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
        string? tenderId = await stripeGateway.ParseCompletedCheckoutEventAsync(payload, signature);
        if (tenderId is null || !Guid.TryParse(tenderId, out Guid id)) return BadRequest();
        if (features.CurrentValue.AutoBiddingEnabled)
        {
            _ = Task.Run(async () =>
            {
                try { await biddingService.GenerateForTenderAsync(id, CancellationToken.None); }
                catch (Exception exception) { LogWebhookGenerationFailed(logger, id, exception); }
            }, CancellationToken.None);
        }
        return Ok();
    }

    [LoggerMessage(EventId = 6001, Level = LogLevel.Error, Message = "Auto-bid generation after checkout failed for tender {TenderId}.")]
    private static partial void LogWebhookGenerationFailed(ILogger logger, Guid tenderId, Exception exception);
}

/// <summary>Body of POST /auto-bid; the optional <c>progressRequestId</c> lets the SPA poll real
/// per-stage progress (Generating → Finalizing → Completed/Failed) while the request is in flight.</summary>
public sealed record StartAutoBidRequest(Guid? ProgressRequestId);
