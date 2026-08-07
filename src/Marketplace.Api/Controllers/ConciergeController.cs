using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Marketplace.Application.Operations;
using Marketplace.Domain.Identity;
using Marketplace.Domain.Rfqs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Marketplace.Api.Controllers;

[ApiController]
[Route("api/v1/admin/concierge")]
[Authorize(Roles = MarketplaceRoles.Administrator)]
public sealed class ConciergeController(IConciergeService concierge) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Overview(CancellationToken cancellationToken) => Ok(await concierge.GetOverviewAsync(cancellationToken));

    [HttpGet("suppliers")]
    public async Task<IActionResult> Suppliers([FromQuery] string? search, CancellationToken cancellationToken) =>
        Ok(await concierge.FindSuppliersAsync(search, cancellationToken));

    [HttpPost("requests/{rfqId:guid}/manual-match")]
    public Task<IActionResult> ManualMatch(Guid rfqId, ManualMatchRequest request, CancellationToken cancellationToken) =>
        Execute(async () => { await concierge.CreateMatchOverrideAsync(UserId(), rfqId, request.SupplierId, request.Reason, cancellationToken); return NoContent(); });

    [HttpPost("requests/{rfqId:guid}/request-correction")]
    public Task<IActionResult> Correction(Guid rfqId, ConciergeNoteRequest request, CancellationToken cancellationToken) =>
        Execute(async () => { await concierge.RequestBuyerCorrectionAsync(UserId(), rfqId, request.Note, cancellationToken); return NoContent(); });

    [HttpPost("offers/{offerId:guid}/request-normalization")]
    public Task<IActionResult> Normalize(Guid offerId, ConciergeNoteRequest request, CancellationToken cancellationToken) =>
        Execute(async () => { await concierge.RequestOfferNormalizationAsync(UserId(), offerId, request.Note, cancellationToken); return NoContent(); });

    [HttpPost("requests/{rfqId:guid}/outcome")]
    public Task<IActionResult> Outcome(Guid rfqId, ConciergeOutcomeRequest request, CancellationToken cancellationToken) =>
        Execute(async () => { await concierge.RecordOutcomeAsync(UserId(), rfqId, request.Outcome, request.Note, cancellationToken); return NoContent(); });

    private Guid UserId()
    {
        string? value = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out Guid id) ? id : throw new UnauthorizedAccessException();
    }
    private static async Task<IActionResult> Execute(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (ConciergeRuleException exception) { return Invalid("concierge", exception.Message); }
        catch (RfqRuleException exception) { return Invalid("outcome", exception.Message); }
        catch (ArgumentException exception) { return Invalid("concierge", exception.Message); }
    }
    private static BadRequestObjectResult Invalid(string field, string message) => new(
        new ValidationProblemDetails(new Dictionary<string, string[]> { [field] = [message] }));
}

public sealed record ManualMatchRequest(Guid SupplierId, [Required, MinLength(10), MaxLength(1000)] string Reason);
public sealed record ConciergeNoteRequest([Required, MinLength(5), MaxLength(1000)] string Note);
public sealed record ConciergeOutcomeRequest(ProcurementOutcome Outcome, [MaxLength(1000)] string? Note);
