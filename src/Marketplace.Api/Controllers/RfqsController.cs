using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Marketplace.Application.Rfqs;
using Marketplace.Domain.Identity;
using Marketplace.Domain.Rfqs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Marketplace.Api.Controllers;

[ApiController]
[Route("api/v1/rfqs")]
[Authorize(Roles = MarketplaceRoles.Buyer)]
public sealed class RfqsController(IRfqService rfqService) : ControllerBase
{
    [HttpGet("mine")]
    public async Task<IActionResult> Mine(CancellationToken cancellationToken) =>
        Ok((await rfqService.GetMineAsync(BuyerId(), cancellationToken)).Select(RfqResponse.From));

    [HttpGet("{rfqId:guid}")]
    public async Task<IActionResult> Get(Guid rfqId, CancellationToken cancellationToken)
    {
        Rfq? rfq = await rfqService.GetAsync(BuyerId(), rfqId, cancellationToken);
        return rfq is null ? NotFound() : Ok(RfqResponse.From(rfq));
    }

    [HttpPost]
    public async Task<IActionResult> Create(RfqRequest request, CancellationToken cancellationToken)
    {
        try
        {
            Rfq rfq = await rfqService.CreateAsync(BuyerId(), request.ToDraft(), cancellationToken);
            return CreatedAtAction(nameof(Get), new { rfqId = rfq.Id }, RfqResponse.From(rfq));
        }
        catch (RfqRuleException exception) { return InvalidRfq(exception.Message); }
    }

    [HttpPut("{rfqId:guid}")]
    public async Task<IActionResult> Update(Guid rfqId, RfqRequest request, CancellationToken cancellationToken)
    {
        try
        {
            Rfq? rfq = await rfqService.UpdateAsync(BuyerId(), rfqId, request.ToDraft(), cancellationToken);
            return rfq is null ? NotFound() : Ok(RfqResponse.From(rfq));
        }
        catch (RfqRuleException exception) { return InvalidRfq(exception.Message); }
    }

    [HttpPost("{rfqId:guid}/publish")]
    public async Task<IActionResult> Publish(Guid rfqId, CancellationToken cancellationToken)
    {
        try
        {
            Rfq? rfq = await rfqService.PublishAsync(BuyerId(), rfqId, cancellationToken);
            return rfq is null ? NotFound() : Ok(RfqResponse.From(rfq));
        }
        catch (RfqRuleException exception) { return InvalidRfq(exception.Message); }
    }

    [HttpPost("{rfqId:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid rfqId, CancellationToken cancellationToken)
    {
        try
        {
            Rfq? rfq = await rfqService.CancelAsync(BuyerId(), rfqId, cancellationToken);
            return rfq is null ? NotFound() : Ok(RfqResponse.From(rfq));
        }
        catch (RfqRuleException exception) { return InvalidRfq(exception.Message); }
    }

    private Guid BuyerId()
    {
        string? subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(subject, out Guid id) ? id : throw new UnauthorizedAccessException();
    }

    private ActionResult InvalidRfq(string message) => ValidationProblem(
        new ValidationProblemDetails(new Dictionary<string, string[]> { ["rfq"] = [message] })
        { Title = "Cererea nu a putut fi salvată." });
}

public sealed record RfqRequest(
    string Title, string? ProjectName, string? Description, Guid PrimaryCategoryId,
    string DeliveryAddress, string DeliveryLocality, string County, int? SupplierSearchRadiusKm, decimal? Latitude, decimal? Longitude,
    DateOnly? PreferredDeliveryDate, DateTimeOffset QuotationDeadline, bool AlternativeProductsAccepted,
    decimal? EstimatedBudget, string? BuyerNotes, IReadOnlyCollection<RfqItemRequest>? Items)
{
    public RfqDraft ToDraft() => new(
        Title ?? string.Empty, ProjectName, Description, PrimaryCategoryId,
        DeliveryAddress ?? string.Empty, DeliveryLocality ?? string.Empty, County ?? string.Empty, SupplierSearchRadiusKm ?? 30,
        Latitude, Longitude, PreferredDeliveryDate, QuotationDeadline, AlternativeProductsAccepted,
        EstimatedBudget, BuyerNotes, Items?.Select(item => item.ToDraft()).ToArray() ?? []);
}

public sealed record RfqItemRequest(
    string ProductName, string? ManufacturerOrBrand, string? Specification, decimal Quantity,
    UnitOfMeasure Unit, bool AlternativesAccepted, string? BuyerNotes)
{
    public RfqItemDraft ToDraft() => new(ProductName ?? string.Empty, ManufacturerOrBrand, Specification, Quantity, Unit, AlternativesAccepted, BuyerNotes);
}

public sealed record RfqResponse(
    Guid Id, Guid BuyerId, string Title, string? ProjectName, string? Description, Guid PrimaryCategoryId,
    string DeliveryAddress, string DeliveryLocality, string County, int SupplierSearchRadiusKm, decimal? Latitude, decimal? Longitude,
    DateOnly? PreferredDeliveryDate, DateTimeOffset QuotationDeadline, bool AlternativeProductsAccepted,
    decimal? EstimatedBudget, string? BuyerNotes, RfqStatus Status, DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt, DateTimeOffset? PublishedAt, DateTimeOffset? ClosedAt, IReadOnlyCollection<RfqItemResponse> Items)
{
    public static RfqResponse From(Rfq rfq) => new(
        rfq.Id, rfq.BuyerId, rfq.Title, rfq.ProjectName, rfq.Description, rfq.PrimaryCategoryId,
        rfq.DeliveryAddress, rfq.DeliveryLocality, rfq.County, rfq.SupplierSearchRadiusKm, rfq.Latitude, rfq.Longitude,
        rfq.PreferredDeliveryDate, rfq.QuotationDeadline, rfq.AlternativeProductsAccepted,
        rfq.EstimatedBudget, rfq.BuyerNotes, rfq.Status, rfq.CreatedAt, rfq.UpdatedAt,
        rfq.PublishedAt, rfq.ClosedAt, rfq.Items.OrderBy(item => item.ItemNumber).Select(RfqItemResponse.From).ToArray());
}

public sealed record RfqItemResponse(
    Guid Id, int ItemNumber, string ProductName, string? ManufacturerOrBrand, string? Specification,
    decimal Quantity, UnitOfMeasure Unit, bool AlternativesAccepted, string? BuyerNotes)
{
    public static RfqItemResponse From(RfqItem item) => new(
        item.Id, item.ItemNumber, item.ProductName, item.ManufacturerOrBrand, item.Specification,
        item.Quantity, item.Unit, item.AlternativesAccepted, item.BuyerNotes);
}
