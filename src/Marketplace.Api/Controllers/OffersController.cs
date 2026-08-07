using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Marketplace.Application.Offers;
using Marketplace.Domain.Identity;
using Marketplace.Domain.Offers;
using Marketplace.Domain.Rfqs;
using Marketplace.Application.Trust;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Marketplace.Api.Controllers;

[ApiController]
[Route("api/v1")]
[Authorize]
public sealed class OffersController(IOfferService offerService, ITrustService trustService) : ControllerBase
{
    [HttpGet("open-rfqs/{rfqId:guid}/offer")]
    [Authorize(Roles = MarketplaceRoles.Supplier)]
    public async Task<IActionResult> MineForRequest(Guid rfqId, CancellationToken cancellationToken)
    {
        Offer? offer = await offerService.GetMineForRfqAsync(UserId(), rfqId, cancellationToken);
        return offer is null ? NoContent() : Ok(OfferResponse.From(offer));
    }

    [HttpGet("offers/mine")]
    [Authorize(Roles = MarketplaceRoles.Supplier)]
    public async Task<IActionResult> Mine(CancellationToken cancellationToken) =>
        Ok((await offerService.GetMineAsync(UserId(), cancellationToken)).Select(OfferSummaryResponse.From));

    [HttpPut("open-rfqs/{rfqId:guid}/offer")]
    [Authorize(Roles = MarketplaceRoles.Supplier)]
    public async Task<IActionResult> Save(Guid rfqId, OfferRequest request, CancellationToken cancellationToken)
    {
        try { return Ok(OfferResponse.From(await offerService.SaveDraftAsync(UserId(), rfqId, request.ToDraft(), cancellationToken))); }
        catch (OfferRuleException exception) { return InvalidOffer(exception.Message); }
    }

    [HttpPost("open-rfqs/{rfqId:guid}/offer/submit")]
    [Authorize(Roles = MarketplaceRoles.Supplier)]
    public async Task<IActionResult> Submit(Guid rfqId, CancellationToken cancellationToken)
    {
        try {
            Offer? offer = await offerService.SubmitAsync(UserId(), rfqId, cancellationToken);
            return offer is null ? NotFound() : Ok(OfferResponse.From(offer));
        }
        catch (RfqConcurrencyException exception) { return StaleRfq(exception.Message); }
        catch (OfferRuleException exception) { return InvalidOffer(exception.Message); }
    }

    [HttpPost("offers/{offerId:guid}/withdraw")]
    [Authorize(Roles = MarketplaceRoles.Supplier)]
    public async Task<IActionResult> Withdraw(Guid offerId, CancellationToken cancellationToken)
    {
        try {
            Offer? offer = await offerService.WithdrawAsync(UserId(), offerId, cancellationToken);
            return offer is null ? NotFound() : Ok(OfferResponse.From(offer));
        } catch (OfferRuleException exception) { return InvalidOffer(exception.Message); }
    }

    [HttpGet("rfqs/{rfqId:guid}/offers")]
    [Authorize(Roles = MarketplaceRoles.Buyer)]
    public async Task<IActionResult> ForBuyer(Guid rfqId, CancellationToken cancellationToken)
    {
        IReadOnlyCollection<BuyerOffer> offers = await offerService.GetForBuyerAsync(UserId(), rfqId, cancellationToken);
        var response = new List<BuyerOfferResponse>();
        foreach (BuyerOffer offer in offers) response.Add(BuyerOfferResponse.From(offer, await trustService.GetFactsAsync(offer.Offer.SupplierId, cancellationToken)));
        return Ok(response);
    }

    [HttpPost("rfqs/{rfqId:guid}/offers/{offerId:guid}/select")]
    [Authorize(Roles = MarketplaceRoles.Buyer)]
    public async Task<IActionResult> Select(Guid rfqId, Guid offerId, CancellationToken cancellationToken)
    {
        try {
            Offer? offer = await offerService.SelectAsync(UserId(), rfqId, offerId, cancellationToken);
            return offer is null ? NotFound() : Ok(OfferResponse.From(offer));
        }
        catch (RfqConcurrencyException exception) { return StaleRfq(exception.Message); }
        catch (Exception exception) when (exception is OfferRuleException or RfqRuleException) {
            return InvalidOffer(exception.Message);
        }
    }

    [HttpPost("rfqs/{rfqId:guid}/reopen")]
    [Authorize(Roles = MarketplaceRoles.Buyer)]
    public async Task<IActionResult> Reopen(Guid rfqId, ReopenRfqRequest request, CancellationToken cancellationToken)
    {
        try {
            Rfq? rfq = await offerService.ReopenAsync(
                UserId(), rfqId, request.Reason ?? string.Empty, request.NewQuotationDeadline, cancellationToken);
            return rfq is null ? NotFound() : Ok(RfqResponse.From(rfq));
        }
        catch (RfqConcurrencyException exception) { return StaleRfq(exception.Message); }
        catch (RfqRuleException exception) {
            return InvalidOffer(exception.Message);
        }
    }

    [HttpPost("open-rfqs/{rfqId:guid}/delivery/report")]
    [Authorize(Roles = MarketplaceRoles.Supplier)]
    public async Task<IActionResult> ReportDelivered(Guid rfqId, CancellationToken cancellationToken)
    {
        try
        {
            Rfq? rfq =
                await offerService.ReportDeliveredAsync(UserId(), rfqId, cancellationToken);
            return rfq is null ? NotFound() : Ok(RfqResponse.From(rfq));
        }
        catch (RfqConcurrencyException exception) { return StaleRfq(exception.Message); }
        catch (RfqRuleException exception)
        {
            return InvalidOffer(exception.Message);
        }
    }

    [HttpPost("rfqs/{rfqId:guid}/delivery/confirm")]
    [Authorize(Roles = MarketplaceRoles.Buyer)]
    public async Task<IActionResult> ConfirmDelivery(Guid rfqId, CancellationToken cancellationToken)
    {
        try
        {
            Rfq? rfq =
                await offerService.ConfirmDeliveryAsync(UserId(), rfqId, cancellationToken);
            return rfq is null ? NotFound() : Ok(RfqResponse.From(rfq));
        }
        catch (RfqConcurrencyException exception) { return StaleRfq(exception.Message); }
        catch (RfqRuleException exception)
        {
            return InvalidOffer(exception.Message);
        }
    }

    private Guid UserId()
    {
        string? subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(subject, out Guid id) ? id : throw new UnauthorizedAccessException();
    }
    private ConflictObjectResult StaleRfq(string message) => Conflict(new ProblemDetails
    {
        Title = "Cererea a fost modificată.",
        Detail = message
    });

    private ActionResult InvalidOffer(string message) => ValidationProblem(new ValidationProblemDetails(
        new Dictionary<string, string[]> { ["offer"] = [message] }) { Title = "Oferta nu a putut fi procesată." });
}

public sealed record OfferRequest(
    OfferPricingMode PricingMode,
    decimal? TotalPrice,
    decimal DeliveryCost,
    int DeliveryDays,
    DateOnly ValidUntil,
    string? PaymentTerms,
    string? Notes,
    IReadOnlyCollection<OfferItemRequest>? Items,
    bool DeliveryIncluded = true,
    OfferCoverage Coverage = OfferCoverage.Full,
    OfferTaxTreatment TaxTreatment = OfferTaxTreatment.NotSpecified,
    decimal? VatRate = null,
    OfferDeliveryTerms? DeliveryTerms = null)
{
    public OfferDraft ToDraft() => new(
        PricingMode,
        TotalPrice,
        DeliveryCost,
        DeliveryDays,
        ValidUntil,
        PaymentTerms,
        Notes,
        Items?.Select(item => item.ToDraft()).ToArray() ?? [],
        DeliveryIncluded,
        Coverage,
        TaxTreatment,
        VatRate,
        DeliveryTerms);
}
public sealed record ReopenRfqRequest(string? Reason, DateTimeOffset? NewQuotationDeadline);
public sealed record OfferItemRequest(Guid RfqItemId, decimal Quantity, decimal UnitPrice, string? ProductDescription, bool Available, bool IsAlternative = false)
{
    public OfferItemDraft ToDraft() => new(RfqItemId, Quantity, UnitPrice, ProductDescription, Available, IsAlternative);
}
public sealed record OfferItemResponse(Guid Id, Guid RfqItemId, decimal Quantity, decimal UnitPrice, string? ProductDescription, bool Available, bool IsAlternative, decimal LineTotal)
{
    public static OfferItemResponse From(OfferItem item) => new(item.Id, item.RfqItemId, item.Quantity, item.UnitPrice, item.ProductDescription, item.Available, item.IsAlternative, item.Available ? item.Quantity * item.UnitPrice : 0);
}
public sealed record OfferResponse(Guid Id, Guid RfqId, Guid SupplierId, OfferStatus Status, OfferPricingMode PricingMode,
    OfferCoverage Coverage, OfferTaxTreatment TaxTreatment, decimal? VatRate, OfferDeliveryTerms DeliveryTerms,
    string Currency, decimal? TotalPrice, bool DeliveryIncluded, decimal DeliveryCost, int DeliveryDays,
    DateOnly ValidUntil, string? PaymentTerms, string? Notes, decimal ItemsTotal, decimal NetTotal, decimal VatAmount,
    decimal Total, bool HasComparableTotal, DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt, DateTimeOffset? SubmittedAt, IReadOnlyCollection<OfferItemResponse> Items)
{
    public static OfferResponse From(Offer offer) => new(offer.Id, offer.RfqId, offer.SupplierId, offer.Status, offer.PricingMode,
        offer.Coverage, offer.TaxTreatment, offer.VatRate, offer.DeliveryTerms,
        offer.Currency, offer.TotalPrice, offer.DeliveryIncluded, offer.DeliveryCost, offer.DeliveryDays, offer.ValidUntil, offer.PaymentTerms, offer.Notes,
        offer.ItemsTotal, offer.NetTotal, offer.VatAmount, offer.Total, offer.HasComparableTotal,
        offer.CreatedAt, offer.UpdatedAt, offer.SubmittedAt, offer.Items.Select(OfferItemResponse.From).ToArray());
}
public sealed record OfferSummaryResponse(
    Guid Id,
    Guid RfqId,
    string RfqTitle,
    DateTimeOffset QuotationDeadline,
    OfferStatus Status,
    OfferCoverage Coverage,
    OfferTaxTreatment TaxTreatment,
    Marketplace.Domain.Rfqs.FulfillmentStatus FulfillmentStatus,
    bool DeliveryIncluded,
    decimal Total,
    bool HasComparableTotal,
    DateTimeOffset UpdatedAt)
{
    public static OfferSummaryResponse From(OfferSummary value) => new(
        value.Offer.Id,
        value.Offer.RfqId,
        value.RfqTitle,
        value.QuotationDeadline,
        value.Offer.Status,
        value.Offer.Coverage,
        value.Offer.TaxTreatment,
        value.FulfillmentStatus,
        value.Offer.DeliveryIncluded,
        value.Offer.Total,
        value.Offer.HasComparableTotal,
        value.Offer.UpdatedAt);
}
public sealed record BuyerOfferResponse(string SupplierName, OfferResponse Offer, OfferCoverageDetails CoverageDetails, TrustFacts? TrustFacts)
{
    public static BuyerOfferResponse From(BuyerOffer value, TrustFacts? trustFacts) => new(value.SupplierName, OfferResponse.From(value.Offer), value.CoverageDetails, trustFacts);
}
