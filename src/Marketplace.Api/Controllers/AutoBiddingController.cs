using Marketplace.Application.Bidding;
using Marketplace.Application.Billing;
using Marketplace.Domain.Seap;
using Marketplace.Infrastructure;
using Marketplace.Infrastructure.Bidding;
using Marketplace.Infrastructure.Billing;
using Marketplace.Infrastructure.Identity;
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
        [FromQuery] string[]? cpvPrefixes, [FromQuery] string[]? counties, CancellationToken cancellationToken) =>
        Ok(await biddingService.ListTendersAsync(categoryIds, radiusKm, county, cancellationToken, cpvPrefixes, counties));

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
    /// Session with dynamic PriceData from the estimated-value tiers (EUR).</summary>
    [HttpPost("tenders/{tenderId:guid}/auto-bid")]
    public async Task<IActionResult> StartAutoBid(Guid tenderId, CancellationToken cancellationToken)
    {
        if (!features.CurrentValue.AutoBiddingEnabled)
            return NotFound();
        SeapTender? tender = await biddingService.GetTenderAsync(tenderId, cancellationToken);
        if (tender is null) return NotFound();
        if (autoBiddingOptions.Value.IsShadowMode)
        {
            BidGenerationResult result = await biddingService.GenerateForTenderAsync(tenderId, cancellationToken);
            return Ok(new { ready = true, shadowMode = true, result });
        }

        ApplicationUser? user = await userManager.FindByIdAsync(UserId.ToString());
        if (user is not null && user.FreeGenerationsRemaining > 0)
        {
            user.FreeGenerationsRemaining -= 1;
            await userManager.UpdateAsync(user);
            BidGenerationResult result = await biddingService.GenerateForTenderAsync(tenderId, cancellationToken);
            return Ok(new { ready = true, shadowMode = false, freeGeneration = true, result });
        }

        int priceEur = PricingCalculator.ComputePriceEur(ToEur(tender.EstimatedValue));
        StripeCheckoutSession session = await stripeGateway.CreateCheckoutSessionAsync(
            new(tender.Id, priceEur, tender.Title), cancellationToken);
        return Ok(new { ready = false, shadowMode = false, checkoutUrl = session.CheckoutUrl, sessionId = session.SessionId, priceEur });
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
