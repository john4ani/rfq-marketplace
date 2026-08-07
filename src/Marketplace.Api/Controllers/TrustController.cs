using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Marketplace.Application.Trust;
using Marketplace.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Marketplace.Api.Controllers;

[ApiController]
[Route("api/v1")]
public sealed class TrustController(ITrustService trustService) : ControllerBase
{
    [Authorize]
    [HttpGet("trust/me")]
    public async Task<IActionResult> Mine(CancellationToken cancellationToken) =>
        CurrentUserId(out Guid accountId)
            ? (await trustService.GetForAccountAsync(accountId, cancellationToken) is { } summary ? Ok(summary) : NotFound())
            : Unauthorized();

    [Authorize]
    [HttpGet("trust/me/facts")]
    public async Task<IActionResult> MyFacts(CancellationToken cancellationToken) =>
        CurrentUserId(out Guid accountId)
            ? (await trustService.GetFactsAsync(accountId, cancellationToken) is { } facts ? Ok(facts) : NotFound())
            : Unauthorized();

    [Authorize(Roles = MarketplaceRoles.Administrator)]
    [HttpGet("admin/trust/accounts")]
    public async Task<IActionResult> Accounts([FromQuery] string? search, CancellationToken cancellationToken) =>
        Ok(await trustService.SearchAsync(search, cancellationToken));

    [Authorize(Roles = MarketplaceRoles.Administrator)]
    [HttpPut("admin/trust/accounts/{accountId:guid}/verification")]
    public async Task<IActionResult> Review(Guid accountId, TrustReviewRequest request, CancellationToken cancellationToken)
    {
        if (!CurrentUserId(out Guid administratorId)) return Unauthorized();
        AccountTrustSummary? summary = await trustService.ReviewAsync(administratorId, accountId,
            new(request.PhoneVerified, request.CompanyVerified, request.Note), cancellationToken);
        return summary is null ? NotFound() : Ok(summary);
    }

    private bool CurrentUserId(out Guid accountId)
    {
        string? subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(subject, out accountId);
    }
}

public sealed record TrustReviewRequest(bool PhoneVerified, bool CompanyVerified, [MaxLength(1000)] string? Note);
