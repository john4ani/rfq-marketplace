using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Marketplace.Application.SupplierDiscovery;
using Marketplace.Application.Companies;
using Marketplace.Domain.Identity;
using Marketplace.Domain.SupplierDiscovery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Marketplace.Api.Controllers;

[ApiController]
[Route("api/v1")]
public sealed class SupplierDiscoveryController(
    ISupplierDiscoveryService discoveryService,
    ICompanyAuthorizationService companyAuthorizationService) : ControllerBase
{
    [Authorize(Roles = MarketplaceRoles.Administrator)]
    [HttpGet("admin/supplier-candidates")]
    public async Task<IActionResult> Candidates(CancellationToken cancellationToken) =>
        Ok(await discoveryService.GetCandidatesAsync(cancellationToken));

    [Authorize(Roles = MarketplaceRoles.Administrator)]
    [HttpGet("admin/discovery-opportunities")]
    public async Task<IActionResult> Opportunities(CancellationToken cancellationToken) =>
        Ok(await discoveryService.GetOpenOpportunitiesAsync(cancellationToken));

    [Authorize(Roles = MarketplaceRoles.Administrator)]
    [HttpPost("admin/supplier-candidates")]
    public async Task<IActionResult> Create(
        SupplierCandidateRequest request,
        CancellationToken cancellationToken)
    {
        if (!CurrentUserId(out Guid administratorId)) return Unauthorized();
        if (!ValidOptionalWebsite(request.Website))
            return ValidationProblem(Errors("website", "Introdu o adresă web completă, de forma https://exemplu.ro."));
        try
        {
            SupplierCandidateView candidate = await discoveryService.CreateCandidateAsync(
                administratorId,
                new(
                    request.CompanyName,
                    request.ContactEmail,
                    request.PhoneNumber,
                    request.Website,
                    request.City,
                    request.County,
                    request.CategoryIds ?? [],
                    request.Source,
                    request.Notes),
                cancellationToken);
            return Created($"/api/v1/admin/supplier-candidates/{candidate.Id}", candidate);
        }
        catch (SupplierCandidateConflictException exception)
        {
            return Conflict(Problem(title: "Firma există deja.", detail: exception.Message));
        }
        catch (SupplierDiscoveryRuleException exception)
        {
            return ValidationProblem(Errors("candidate", exception.Message));
        }
    }

    [Authorize(Roles = MarketplaceRoles.Administrator)]
    [HttpPut("admin/supplier-candidates/{candidateId:guid}/review")]
    public async Task<IActionResult> Review(
        Guid candidateId,
        SupplierCandidateReviewRequest request,
        CancellationToken cancellationToken)
    {
        if (!CurrentUserId(out Guid administratorId)) return Unauthorized();
        try
        {
            SupplierCandidateView? candidate = await discoveryService.ReviewCandidateAsync(
                administratorId, candidateId, request.Approved, request.Notes, cancellationToken);
            return candidate is null ? NotFound() : Ok(candidate);
        }
        catch (SupplierDiscoveryRuleException exception)
        {
            return ValidationProblem(Errors("review", exception.Message));
        }
        catch (SupplierDiscoveryConcurrencyException exception)
        {
            return Conflict(Problem(title: "Firma a fost modificată.", detail: exception.Message));
        }
    }

    [Authorize(Roles = MarketplaceRoles.Administrator)]
    [HttpPost("admin/supplier-candidates/{candidateId:guid}/invitations")]
    public async Task<IActionResult> Invite(
        Guid candidateId,
        SupplierInvitationRequest request,
        CancellationToken cancellationToken)
    {
        if (!CurrentUserId(out Guid administratorId)) return Unauthorized();
        try
        {
            SupplierInvitationCreated? invitation = await discoveryService.CreateInvitationAsync(
                administratorId, candidateId, request.RfqId, cancellationToken);
            return invitation is null ? NotFound() : Created(
                $"/api/v1/supplier-invitations/{invitation.Token}",
                invitation);
        }
        catch (SupplierCandidateConflictException exception)
        {
            return Conflict(Problem(title: "Invitația există deja.", detail: exception.Message));
        }
        catch (SupplierDiscoveryRuleException exception)
        {
            return ValidationProblem(Errors("invitation", exception.Message));
        }
        catch (SupplierDiscoveryConcurrencyException exception)
        {
            return Conflict(Problem(title: "Firma sau invitația a fost modificată.", detail: exception.Message));
        }
    }

    [AllowAnonymous]
    [HttpGet("supplier-invitations/{token}")]
    public async Task<IActionResult> Invitation(string token, CancellationToken cancellationToken)
    {
        SupplierInvitationView? invitation = await discoveryService.GetInvitationAsync(token, cancellationToken);
        return invitation is null ? NotFound(Problem(title: "Invitația nu există.")) : Ok(invitation);
    }

    [Authorize]
    [HttpPost("supplier-invitations/{token}/claim")]
    public async Task<IActionResult> Claim(string token, [FromQuery] Guid? companyId, CancellationToken cancellationToken)
    {
        if (!CurrentUserId(out Guid actorId)) return Unauthorized();
        CompanyContextResolution context = await companyAuthorizationService.ResolveCompanyContextAsync(actorId, companyId, cancellationToken);
        if (context.Status != CompanyContextStatus.Resolved)
            return context.Status == CompanyContextStatus.Ambiguous
                ? Conflict(Problem(title: "Alege compania care revendică invitația."))
                : context.Status == CompanyContextStatus.Unavailable
                    ? ValidationProblem(Errors("claim", "Completează profilul de furnizor înainte de a revendica firma."))
                    : Forbid();
        try
        {
            SupplierInvitationView? invitation = await discoveryService.ClaimAsync(
                actorId, context.CompanyId!.Value, token, cancellationToken);
            return invitation is null ? NotFound(Problem(title: "Invitația nu există.")) : Ok(invitation);
        }
        catch (SupplierDiscoveryRuleException exception)
        {
            return ValidationProblem(Errors("claim", exception.Message));
        }
        catch (SupplierDiscoveryConcurrencyException exception)
        {
            return Conflict(Problem(title: "Invitația a fost folosită între timp.", detail: exception.Message));
        }
    }

    private bool CurrentUserId(out Guid userId)
    {
        string? subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(subject, out userId);
    }

    private static bool ValidOptionalWebsite(string? website) =>
        string.IsNullOrWhiteSpace(website)
        || Uri.TryCreate(website.Trim(), UriKind.Absolute, out Uri? uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    private static ValidationProblemDetails Errors(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] })
        {
            Title = "Operația de discovery nu a putut fi finalizată."
        };
}

public sealed record SupplierCandidateRequest(
    [Required, MinLength(2), MaxLength(180)] string CompanyName,
    [EmailAddress, MaxLength(254)] string? ContactEmail,
    [MaxLength(40)] string? PhoneNumber,
    [MaxLength(500)] string? Website,
    [Required, MinLength(2), MaxLength(120)] string City,
    [Required, MinLength(2), MaxLength(120)] string County,
    IReadOnlyCollection<Guid>? CategoryIds,
    [Required, MinLength(3), MaxLength(500)] string Source,
    [MaxLength(1000)] string? Notes);
public sealed record SupplierCandidateReviewRequest(bool Approved, [Required, MinLength(5), MaxLength(1000)] string Notes);
public sealed record SupplierInvitationRequest(Guid RfqId);
