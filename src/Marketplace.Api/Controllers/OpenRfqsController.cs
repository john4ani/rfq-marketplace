using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Marketplace.Application.Rfqs;
using Marketplace.Domain.Identity;
using Marketplace.Domain.Rfqs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Marketplace.Api.Controllers;

[ApiController]
[Route("api/v1/open-rfqs")]
[Authorize(Roles = MarketplaceRoles.Supplier)]
public sealed class OpenRfqsController(IRfqService rfqService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? search,
        [FromQuery] Guid? categoryId,
        CancellationToken cancellationToken)
    {
        if (search?.Length > 120)
            return ValidationProblem(new ValidationProblemDetails(
                new Dictionary<string, string[]> { ["search"] = ["Căutarea poate avea cel mult 120 de caractere."] }));
        IReadOnlyCollection<OpenRfq> results =
            await rfqService.GetOpenAsync(SupplierId(), search, categoryId, cancellationToken);
        return Ok(results.Select(OpenRfqResponse.From));
    }

    [HttpGet("{rfqId:guid}")]
    public async Task<IActionResult> Get(Guid rfqId, CancellationToken cancellationToken)
    {
        OpenRfq? result = await rfqService.GetOpenAsync(SupplierId(), rfqId, cancellationToken);
        return result is null ? NotFound() : Ok(OpenRfqResponse.From(result));
    }

    private Guid SupplierId()
    {
        string? subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(subject, out Guid id) ? id : throw new UnauthorizedAccessException();
    }
}

public sealed record OpenRfqResponse(
    Guid Id,
    string Title,
    string? ProjectName,
    string? Description,
    Guid PrimaryCategoryId,
    string CategoryName,
    string BuyerDisplayName,
    string DeliveryLocality,
    string County,
    DateOnly? PreferredDeliveryDate,
    DateTimeOffset QuotationDeadline,
    bool AlternativeProductsAccepted,
    decimal? EstimatedBudget,
    DateTimeOffset? PublishedAt,
    bool IsPartialMatch,
    IReadOnlyCollection<RfqItemResponse> Items)
{
    public static OpenRfqResponse From(OpenRfq result)
    {
        Rfq rfq = result.Rfq;
        return new(
            rfq.Id,
            rfq.Title,
            rfq.ProjectName,
            rfq.Description,
            rfq.PrimaryCategoryId,
            result.CategoryName,
            result.BuyerDisplayName,
            rfq.DeliveryLocality,
            rfq.County,
            rfq.PreferredDeliveryDate,
            rfq.QuotationDeadline,
            rfq.AlternativeProductsAccepted,
            rfq.EstimatedBudget,
            rfq.PublishedAt,
            result.IsPartialMatch,
            rfq.Items.Where(item => result.MatchingItemIds is null || result.MatchingItemIds.Contains(item.Id))
                .OrderBy(item => item.ItemNumber).Select(RfqItemResponse.From).ToArray());
    }
}
