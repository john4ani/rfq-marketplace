using Marketplace.Application.Bidding;
using Marketplace.Application.Billing;
using Marketplace.Domain.Seap;
using Marketplace.Infrastructure;
using Marketplace.Infrastructure.Bidding;
using Marketplace.Infrastructure.Billing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Marketplace.Api.Controllers;

[ApiController]
[Route("api/v1/seap")]
[Authorize]
public sealed partial class AutoBiddingController(
    IBiddingGenerationService biddingService,
    IStripeGateway stripeGateway,
    IOptions<AutoBiddingOptions> autoBiddingOptions,
    IOptionsMonitor<FeaturesOptions> features,
    ILogger<AutoBiddingController> logger) : ControllerBase
{
    [HttpGet("tenders")]
    public async Task<IActionResult> ListTenders(
        [FromQuery] Guid[]? categoryIds, [FromQuery] int? radiusKm, [FromQuery] string? county, CancellationToken cancellationToken) =>
        Ok(await biddingService.ListTendersAsync(categoryIds, radiusKm, county, cancellationToken));

    [HttpGet("tenders/{tenderId:guid}")]
    public async Task<IActionResult> GetTender(Guid tenderId, CancellationToken cancellationToken)
    {
        SeapTender? tender = await biddingService.GetTenderAsync(tenderId, cancellationToken);
        return tender is null ? NotFound() : Ok(tender);
    }

    /// <summary>Starts auto-bid: in Shadow Mode it generates silently without charging; otherwise it
    /// creates a Stripe Checkout Session with dynamic PriceData based on the tender's estimated value.</summary>
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
        int priceEur = PricingCalculator.ComputePriceEur(tender.EstimatedValue ?? 0m);
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
