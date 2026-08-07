using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Marketplace.Application.Rfqs;
using Marketplace.Application.Trust;
using Marketplace.Domain.Identity;
using Marketplace.Domain.Rfqs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Marketplace.Api.Controllers;

[ApiController]
[Route("api/v1/open-rfqs")]
[Authorize(Roles = MarketplaceRoles.Supplier)]
public sealed class OpenRfqsController(IRfqService rfqService, ITrustService trustService) : ControllerBase
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
        var response = new List<OpenRfqResponse>();
        foreach (OpenRfq item in results) response.Add(OpenRfqResponse.From(item, await trustService.GetFactsAsync(item.Rfq.BuyerId, cancellationToken)));
        return Ok(response);
    }

    [HttpGet("{rfqId:guid}")]
    public async Task<IActionResult> Get(Guid rfqId, CancellationToken cancellationToken)
    {
        OpenRfq? result = await rfqService.GetOpenAsync(SupplierId(), rfqId, cancellationToken);
        return result is null ? NotFound() : Ok(OpenRfqResponse.From(result, await trustService.GetFactsAsync(result.Rfq.BuyerId, cancellationToken)));
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
    IReadOnlyCollection<Guid> CategoryIds,
    string CategoryName,
    string BuyerDisplayName,
    string DeliveryLocality,
    string County,
    DateOnly? PreferredDeliveryDate,
    DateTimeOffset QuotationDeadline,
    bool AlternativeProductsAccepted,
    bool DeliveryRequired,
    decimal? EstimatedBudget,
    DateTimeOffset? PublishedAt,
    bool IsPartialMatch,
    ProcurementNeedSource Source,
    ProcurementInputType InputType,
    ProcurementNeedType NeedType,
    Guid? ProjectTemplateId,
    ProcurementNeedStatus NeedStatus,
    ProjectContextResponse? ProjectContext,
    IReadOnlyCollection<RfqItemResponse> Items,
    TrustFacts? BuyerTrustFacts)
{
    public static OpenRfqResponse From(OpenRfq result, TrustFacts? buyerTrustFacts)
    {
        Rfq rfq = result.Rfq;
        return new(
            rfq.Id,
            rfq.Title,
            rfq.ProjectName,
            rfq.Description,
            rfq.PrimaryCategoryId,
            rfq.RequiredCategoryIds,
            result.CategoryName,
            result.BuyerDisplayName,
            rfq.DeliveryLocality,
            rfq.County,
            rfq.PreferredDeliveryDate,
            rfq.QuotationDeadline,
            rfq.AlternativeProductsAccepted,
            rfq.DeliveryRequired,
            rfq.EstimatedBudget,
            rfq.PublishedAt,
            result.IsPartialMatch,
            rfq.Source,
            rfq.InputType,
            rfq.NeedType,
            rfq.ProjectTemplateId,
            rfq.NeedStatus,
            ProjectContextResponse.From(rfq.ProjectContext),
            rfq.Items.Where(item => result.MatchingItemIds is null || result.MatchingItemIds.Contains(item.Id))
                .OrderBy(item => item.ItemNumber).Select(RfqItemResponse.From).ToArray(), buyerTrustFacts);
    }
}
