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
    [HttpGet("admin/supplier-discovery/needs/{rfqId:guid}")]
    public async Task<IActionResult> NeedReview(Guid rfqId, CancellationToken cancellationToken) =>
        (await discoveryService.GetNeedReviewAsync(rfqId, cancellationToken)) is { } review ? Ok(review) : NotFound();

    [Authorize(Roles = MarketplaceRoles.Administrator)]
    [HttpGet("admin/supplier-discovery/acquisition")]
    public async Task<IActionResult> Acquisition([FromQuery] string? county, [FromQuery] Guid? categoryId, CancellationToken cancellationToken) =>
        Ok(await discoveryService.GetAcquisitionCandidatesAsync(county, categoryId, cancellationToken));

    [Authorize(Roles = MarketplaceRoles.Administrator)]
    [HttpPut("admin/supplier-discovery/opportunity-matches/{matchId:guid}/review")]
    public async Task<IActionResult> ReviewMatch(Guid matchId, OpportunityMatchReviewRequest request, CancellationToken cancellationToken)
    {
        if (!CurrentUserId(out Guid administratorId)) return Unauthorized();
        try { return (await discoveryService.ReviewOpportunityMatchAsync(administratorId, matchId, request.Approved, request.Note, cancellationToken)) is { } match ? Ok(match) : NotFound(); }
        catch (SupplierDiscoveryRuleException exception) { return ValidationProblem(Errors("review", exception.Message)); }
    }

    [Authorize(Roles = MarketplaceRoles.Administrator)]
    [HttpPost("admin/supplier-discovery/needs/{rfqId:guid}/candidates/{candidateId:guid}/match")]
    public async Task<IActionResult> CreateCandidateMatch(Guid rfqId, Guid candidateId, CancellationToken cancellationToken)
    {
        if (!CurrentUserId(out Guid administratorId)) return Unauthorized();
        try { return (await discoveryService.CreateCandidateMatchAsync(administratorId, rfqId, candidateId, cancellationToken)) is { } match ? Ok(match) : NotFound(); }
        catch (SupplierDiscoveryRuleException exception) { return ValidationProblem(Errors("match", exception.Message)); }
    }

    [Authorize(Roles = MarketplaceRoles.Administrator)]
    [HttpPost("admin/supplier-candidates/{candidateId:guid}/outreach/email")]
    public async Task<IActionResult> SendEmail(Guid candidateId, SupplierOutreachRequest request, CancellationToken cancellationToken)
    {
        if (!CurrentUserId(out Guid administratorId)) return Unauthorized();
        try { return (await discoveryService.SendEmailInvitationAsync(administratorId, candidateId, request.OpportunityMatchId, cancellationToken)) is { } result ? Ok(result) : NotFound(); }
        catch (SupplierDiscoveryRuleException exception) { return ValidationProblem(Errors("outreach", exception.Message)); }
    }

    [Authorize(Roles = MarketplaceRoles.Administrator)]
    [HttpPost("admin/supplier-candidates/{candidateId:guid}/outreach/manual")]
    public async Task<IActionResult> RecordManualContact(Guid candidateId, SupplierManualContactRequest request, CancellationToken cancellationToken)
    {
        if (!CurrentUserId(out Guid administratorId)) return Unauthorized();
        try { return (await discoveryService.RecordManualContactAsync(administratorId, candidateId, request.OpportunityMatchId, request.Channel, request.Note, cancellationToken)) is { } result ? Ok(result) : NotFound(); }
        catch (SupplierDiscoveryRuleException exception) { return ValidationProblem(Errors("outreach", exception.Message)); }
    }

    [Authorize(Roles = MarketplaceRoles.Administrator)]
    [HttpPost("admin/supplier-candidates/{candidateId:guid}/suppress-contact")]
    public async Task<IActionResult> SuppressContact(Guid candidateId, SupplierContactSuppressionRequest request, CancellationToken cancellationToken)
    {
        if (!CurrentUserId(out Guid administratorId)) return Unauthorized();
        return (await discoveryService.SuppressContactAsync(administratorId, candidateId, request.Reason, cancellationToken)) is { } result ? Ok(result) : NotFound();
    }

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

    [AllowAnonymous]
    [HttpPost("supplier-outreach/opt-out/{token}")]
    public async Task<IActionResult> OptOut(string token, CancellationToken cancellationToken) =>
        await discoveryService.OptOutEmailAsync(token, cancellationToken) ? NoContent() : NotFound();

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
public sealed record OpportunityMatchReviewRequest(bool Approved, [MaxLength(1000)] string? Note);
public sealed record SupplierOutreachRequest(Guid OpportunityMatchId);
public sealed record SupplierManualContactRequest(Guid OpportunityMatchId, SupplierOutreachChannel Channel, [MaxLength(1000)] string? Note);
public sealed record SupplierContactSuppressionRequest([MaxLength(500)] string? Reason);
